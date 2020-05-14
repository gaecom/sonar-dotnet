﻿/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2020 SonarSource SA
 * mailto: contact AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [Rule(DiagnosticId)]
    public sealed class ClassNotInstantiatable : ClassNotInstantiatableBase
    {
        private static readonly DiagnosticDescriptor rule =
            DiagnosticDescriptorBuilder.GetDescriptor(DiagnosticId, MessageFormat, RspecStrings.ResourceManager);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(rule);

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSymbolAction(CheckClassWithOnlyUnusedPrivateConstructors, SymbolKind.NamedType);
        }

        private static void CheckClassWithOnlyUnusedPrivateConstructors(SymbolAnalysisContext context)
        {
            var namedType = context.Symbol as INamedTypeSymbol;
            if (!IsNonStaticClassWithNoAttributes(namedType) || DerivesFromSafeHandle(namedType))
            {
                return;
            }

            var members = namedType.GetMembers();
            var constructors = GetConstructors(members).ToList();

            if (!HasOnlyCandidateConstructors(constructors) ||
                HasOnlyStaticMembers(members.Except(constructors).ToList()))
            {
                return;
            }

            var typeDeclarations = new CSharpRemovableDeclarationCollector(namedType, context.Compilation).TypeDeclarations;

            if (!IsAnyConstructorCalled<BaseTypeDeclarationSyntax, ObjectCreationExpressionSyntax, ClassDeclarationSyntax>
                (namedType, typeDeclarations))
            {
                var message = constructors.Count > 1
                    ? "at least one of its constructors"
                    : "its constructor";

                foreach (var classDeclaration in typeDeclarations)
                {
                    context.ReportDiagnosticIfNonGenerated(
                        Diagnostic.Create(rule, classDeclaration.SyntaxNode.Identifier.GetLocation(), message));
                }
            }
        }
    }
}
