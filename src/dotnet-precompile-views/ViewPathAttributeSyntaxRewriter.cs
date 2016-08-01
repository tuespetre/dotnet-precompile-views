using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace dotnet_precompile_views
{
    public class ViewPathAttributeSyntaxRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel semanticModel;

        public ViewPathAttributeSyntaxRewriter(SemanticModel semanticModel)
        {
            this.semanticModel = semanticModel;
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            foreach (var type in node.BaseList.Types)
            {
                var typeInfo = semanticModel.GetTypeInfo(type.Type);
                var name = typeof(IRazorPage).FullName;
                var desired = semanticModel.Compilation.GetTypeByMetadataName(name);

                if (typeInfo.Type.AllInterfaces.Any(i => i == desired))
                {
                    var attributeNamespace = typeof(ViewPathAttribute).Namespace;
                    var attributeName = typeof(ViewPathAttribute).Name;
                    var attrName = SyntaxFactory.QualifiedName(
                        SyntaxFactory.IdentifierName(attributeNamespace),
                        SyntaxFactory.IdentifierName(attributeName));
                    var literalToken = SyntaxFactory.Literal(node.SyntaxTree.FilePath);
                    var exprToken = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, literalToken);
                    var pathArg = SyntaxFactory.AttributeArgument(exprToken);
                    var argList = SyntaxFactory.AttributeArgumentList(new SeparatedSyntaxList<AttributeArgumentSyntax>().Add(pathArg));
                    var attribute = SyntaxFactory.Attribute(attrName, argList);
                    var attributeList = SyntaxFactory.AttributeList(new SeparatedSyntaxList<AttributeSyntax>().Add(attribute));
                    return node.AddAttributeLists(attributeList);
                }
            }

            return base.VisitClassDeclaration(node);
        }
    }
}
