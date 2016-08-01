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
                var razorPageInterface = semanticModel.Compilation.GetTypeByMetadataName(typeof(IRazorPage).FullName);

                if (typeInfo.Type.AllInterfaces.Any(i => i == razorPageInterface))
                {
                    var attributeName = 
                        SyntaxFactory.QualifiedName(
                            SyntaxFactory.IdentifierName(typeof(ViewPathAttribute).Namespace),
                            SyntaxFactory.IdentifierName(typeof(ViewPathAttribute).Name));

                    var pathArgument = 
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(node.SyntaxTree.FilePath)));
                    
                    var attributeArgumentList = 
                        SyntaxFactory.AttributeArgumentList(
                            new SeparatedSyntaxList<AttributeArgumentSyntax>().Add(pathArgument));

                    var attribute = 
                            SyntaxFactory.Attribute(attributeName, attributeArgumentList);

                    var attributeList = 
                        SyntaxFactory.AttributeList(
                            new SeparatedSyntaxList<AttributeSyntax>().Add(attribute));

                    return node.AddAttributeLists(attributeList);
                }
            }

            return base.VisitClassDeclaration(node);
        }
    }
}
