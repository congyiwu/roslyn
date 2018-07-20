﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.Operations
{
    public static partial class OperationExtensions
    {
        /// <summary>
        /// This will check whether context around the operation has any error such as syntax or semantic error
        /// </summary>
        internal static bool HasErrors(this IOperation operation, Compilation compilation, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (compilation == null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            // once we made sure every operation has Syntax, we will remove this condition
            if (operation.Syntax == null)
            {
                return true;
            }

            // if wrong compilation is given, GetSemanticModel will throw due to tree not belong to the given compilation.
            var model = operation.SemanticModel ?? compilation.GetSemanticModel(operation.Syntax.SyntaxTree);
            if (model.IsSpeculativeSemanticModel)
            {
                // GetDiagnostics not supported for speculative semantic model.
                // https://github.com/dotnet/roslyn/issues/28075
                return false;
            }

            return model.GetDiagnostics(operation.Syntax.Span, cancellationToken).Any(d => d.DefaultSeverity == DiagnosticSeverity.Error);
        }

        /// <summary>
        /// Returns all the descendant operations of the given <paramref name="operation"/> in evaluation order.
        /// </summary>
        /// <param name="operation">Operation whose descendants are to be fetched.</param>
        public static IEnumerable<IOperation> Descendants(this IOperation operation)
        {
            return Descendants(operation, includeSelf: false);
        }

        /// <summary>
        /// Returns all the descendant operations of the given <paramref name="operation"/> including the given <paramref name="operation"/> in evaluation order.
        /// </summary>
        /// <param name="operation">Operation whose descendants are to be fetched.</param>
        public static IEnumerable<IOperation> DescendantsAndSelf(this IOperation operation)
        {
            return Descendants(operation, includeSelf: true);
        }

        private static IEnumerable<IOperation> Descendants(IOperation operation, bool includeSelf)
        {
            if (operation == null)
            {
                yield break;
            }

            if (includeSelf)
            {
                yield return operation;
            }

            var stack = ArrayBuilder<IEnumerator<IOperation>>.GetInstance();
            stack.Push(operation.Children.GetEnumerator());

            while (stack.Any())
            {
                var iterator = stack.Pop();

                if (!iterator.MoveNext())
                {
                    continue;
                }

                var current = iterator.Current;

                // push current iterator back in to the stack
                stack.Push(iterator);

                // push children iterator to the stack
                if (current != null)
                {
                    yield return current;
                    stack.Push(current.Children.GetEnumerator());
                }
            }

            stack.Free();
        }

        /// <summary>
        /// Gets all the declared local variables in the given <paramref name="declarationGroup"/>.
        /// </summary>
        /// <param name="declarationGroup">Variable declaration group</param>
        public static ImmutableArray<ILocalSymbol> GetDeclaredVariables(this IVariableDeclarationGroupOperation declarationGroup)
        {
            if (declarationGroup == null)
            {
                throw new ArgumentNullException(nameof(declarationGroup));
            }

            var arrayBuilder = ArrayBuilder<ILocalSymbol>.GetInstance();
            foreach (IVariableDeclarationOperation group in declarationGroup.Declarations)
            {
                group.GetDeclaredVariables(arrayBuilder);
            }

            return arrayBuilder.ToImmutableAndFree();
        }

        /// <summary>
        /// Gets all the declared local variables in the given <paramref name="declaration"/>.
        /// </summary>
        /// <param name="declaration">Variable declaration</param>
        public static ImmutableArray<ILocalSymbol> GetDeclaredVariables(this IVariableDeclarationOperation declaration)
        {
            if (declaration == null)
            {
                throw new ArgumentNullException(nameof(declaration));
            }

            var arrayBuilder = ArrayBuilder<ILocalSymbol>.GetInstance();
            declaration.GetDeclaredVariables(arrayBuilder);
            return arrayBuilder.ToImmutableAndFree();
        }

        private static void GetDeclaredVariables(this IVariableDeclarationOperation declaration, ArrayBuilder<ILocalSymbol> arrayBuilder)
        {
            foreach (var decl in declaration.Declarators)
            {
                arrayBuilder.Add(decl.Symbol);
            }
        }

        /// <summary>
        /// Gets the variable initialzer for the given <paramref name="declarationOperation"/>, checking to see if there is a parent initializer
        /// if the single variable initializer is null.
        /// </summary>
        /// <param name="declarationOperation">Single variable declaration to retrieve initializer for.</param>
        public static IVariableInitializerOperation GetVariableInitializer(this IVariableDeclaratorOperation declarationOperation)
        {
            if (declarationOperation == null)
            {
                throw new ArgumentNullException(nameof(declarationOperation));
            }

            return declarationOperation.Initializer ?? (declarationOperation.Parent as IVariableDeclarationOperation)?.Initializer;
        }

        /// <summary>
        /// Get an optional argument name for a named argument to the given <paramref name="dynamicOperation"/> at the given <paramref name="index"/>.
        /// </summary>
        /// <param name="dynamicOperation">Dynamic or late bound operation.</param>
        /// <param name="index">Argument index.</param>
        public static string GetArgumentName(this IDynamicInvocationOperation dynamicOperation, int index)
        {
            if (dynamicOperation == null)
            {
                throw new ArgumentNullException(nameof(dynamicOperation));
            }

            return GetArgumentName((HasDynamicArgumentsExpression)dynamicOperation, index);
        }

        /// <summary>
        /// Get an optional argument name for a named argument to the given <paramref name="dynamicOperation"/> at the given <paramref name="index"/>.
        /// </summary>
        /// <param name="dynamicOperation">Dynamic or late bound operation.</param>
        /// <param name="index">Argument index.</param>
        public static string GetArgumentName(this IDynamicIndexerAccessOperation dynamicOperation, int index)
        {
            if (dynamicOperation == null)
            {
                throw new ArgumentNullException(nameof(dynamicOperation));
            }

            return GetArgumentName((HasDynamicArgumentsExpression)dynamicOperation, index);
        }

        /// <summary>
        /// Get an optional argument name for a named argument to the given <paramref name="dynamicOperation"/> at the given <paramref name="index"/>.
        /// </summary>
        /// <param name="dynamicOperation">Dynamic or late bound operation.</param>
        /// <param name="index">Argument index.</param>
        public static string GetArgumentName(this IDynamicObjectCreationOperation dynamicOperation, int index)
        {
            if (dynamicOperation == null)
            {
                throw new ArgumentNullException(nameof(dynamicOperation));
            }

            return GetArgumentName((HasDynamicArgumentsExpression)dynamicOperation, index);
        }

        /// <summary>
        /// Get an optional argument name for a named argument to the given <paramref name="dynamicOperation"/> at the given <paramref name="index"/>.
        /// </summary>
        /// <param name="dynamicOperation">Dynamic or late bound operation.</param>
        /// <param name="index">Argument index.</param>
        internal static string GetArgumentName(this HasDynamicArgumentsExpression dynamicOperation, int index)
        {
            if (dynamicOperation.Arguments.IsDefaultOrEmpty)
            {
                throw new InvalidOperationException();
            }

            if (index < 0 || index >= dynamicOperation.Arguments.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var argumentNames = dynamicOperation.ArgumentNames;
            return argumentNames.IsDefaultOrEmpty ? null : argumentNames[index];
        }

        /// <summary>
        /// Get an optional argument <see cref="RefKind"/> for an argument at the given <paramref name="index"/> to the given <paramref name="dynamicOperation"/>.
        /// Returns a non-null argument <see cref="RefKind"/> for C#.
        /// Always returns null for VB as <see cref="RefKind"/> cannot be specified for an the argument in VB.
        /// </summary>
        /// <param name="dynamicOperation">Dynamic or late bound operation.</param>
        /// <param name="index">Argument index.</param>
        public static RefKind? GetArgumentRefKind(this IDynamicInvocationOperation dynamicOperation, int index)
        {
            if (dynamicOperation == null)
            {
                throw new ArgumentNullException(nameof(dynamicOperation));
            }

            return GetArgumentRefKind((HasDynamicArgumentsExpression)dynamicOperation, index);
        }

        /// <summary>
        /// Get an optional argument <see cref="RefKind"/> for an argument at the given <paramref name="index"/> to the given <paramref name="dynamicOperation"/>.
        /// Returns a non-null argument <see cref="RefKind"/> for C#.
        /// Always returns null for VB as <see cref="RefKind"/> cannot be specified for an the argument in VB.
        /// </summary>
        /// <param name="dynamicOperation">Dynamic or late bound operation.</param>
        /// <param name="index">Argument index.</param>
        public static RefKind? GetArgumentRefKind(this IDynamicIndexerAccessOperation dynamicOperation, int index)
        {
            if (dynamicOperation == null)
            {
                throw new ArgumentNullException(nameof(dynamicOperation));
            }

            return GetArgumentRefKind((HasDynamicArgumentsExpression)dynamicOperation, index);
        }

        /// <summary>
        /// Get an optional argument <see cref="RefKind"/> for an argument at the given <paramref name="index"/> to the given <paramref name="dynamicOperation"/>.
        /// Returns a non-null argument <see cref="RefKind"/> for C#.
        /// Always returns null for VB as <see cref="RefKind"/> cannot be specified for an the argument in VB.
        /// </summary>
        /// <param name="dynamicOperation">Dynamic or late bound operation.</param>
        /// <param name="index">Argument index.</param>
        public static RefKind? GetArgumentRefKind(this IDynamicObjectCreationOperation dynamicOperation, int index)
        {
            if (dynamicOperation == null)
            {
                throw new ArgumentNullException(nameof(dynamicOperation));
            }

            return GetArgumentRefKind((HasDynamicArgumentsExpression)dynamicOperation, index);
        }

        internal static RefKind? GetArgumentRefKind(this HasDynamicArgumentsExpression dynamicOperation, int index)
        {
            if (dynamicOperation.Arguments.IsDefaultOrEmpty)
            {
                throw new InvalidOperationException();
            }

            if (index < 0 || index >= dynamicOperation.Arguments.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var argumentRefKinds = dynamicOperation.ArgumentRefKinds;
            if (argumentRefKinds.IsDefault)
            {
                // VB case, arguments cannot have RefKind.
                return null;
            }

            if (argumentRefKinds.IsEmpty)
            {
                // C# case where no explicit RefKind was specified for any argument, hence all arguments have RefKind.None.
                return RefKind.None;
            }

            return argumentRefKinds[index];
        }

        /// <summary>
        /// Gets the root operation for the <see cref="IOperation"/> tree containing the given <paramref name="operation"/>.
        /// </summary>
        /// <param name="operation">Operation whose root is requested.</param>
        internal static IOperation GetRootOperation(this IOperation operation)
        {
            Debug.Assert(operation != null);

            while (operation.Parent != null)
            {
                operation = operation.Parent;
            }

            return operation;
        }

        /// <summary>
        /// Gets a loop operation that corresponds to the given branch operation.
        /// </summary>
        /// <param name="branchOperation">the branch operation for which a corresponding loop is looked up</param>
        /// <returns>the corresponding loop operation or <c>null</c> in case not found (e.g. no loop syntax or the branch
        /// belongs switch instead of loop operation)</returns>
        public static ILoopOperation GetCorrespondingLoop(this IBranchOperation branchOperation)
        {
            if (branchOperation.BranchKind != BranchKind.Break && branchOperation.BranchKind != BranchKind.Continue)
            {
                throw new InvalidOperationException("Invalid branch kind type. Finding a corresponding loop requires " +
                    "'break' or 'continue' kinds, but the current branch kind provided is '{branchOperation.Kind}'.");
            }

            return FindCorrespondingOperation<ILoopOperation>(branchOperation, 
                op => op is ISwitchOperation && branchOperation.BranchKind == BranchKind.Break);
        }

        /// <summary>
        /// Gets a switch operation that corresponds to the given branch operation.
        /// </summary>
        /// <param name="branchOperation">the branch operation for which a corresponding switch is looked up</param>
        /// <returns>the corresponding switch operation or <c>null</c> in case not found (e.g. no switch syntax or the branch
        /// belongs loop instead of switch operation)</returns>
        public static ISwitchOperation GetCorrespondingSwitch(this IBranchOperation branchOperation)
        {
            if (branchOperation.BranchKind != BranchKind.Break)
            {
                throw new InvalidOperationException("Invalid branch kind type. Finding a corresponding switch requires " +
                    "'break' kind, but the current branch kind provided is '{branchOperation.Kind}'.");
            }

            return FindCorrespondingOperation<ISwitchOperation>(branchOperation, op => op is ILoopOperation);
        }

        private static T FindCorrespondingOperation<T>(IOperation operation, Func<IOperation, bool> shouldStopAscending) where T : IOperation
        {
            for (var current = operation; current.Parent != null; current = current.Parent)
            {
                if (current is T corresponding)
                {
                    return corresponding;
                }

                if (shouldStopAscending(current))
                {
                    return default;
                }
            }

            return default;
        }
    }
}
