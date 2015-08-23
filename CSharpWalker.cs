using Roslyn.Compilers.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XSLT_Transformer
{
    public class CSharpWalker : SyntaxWalker
    {
        private TextWriter Output;
        private string namespaceName;
        private string className;

        int Tabs = 0;
        private bool writeOut=true;
        private string indents = "";
        private List<string> nodeNames;

        public CSharpWalker(TextWriter writer, List<string> nodeNames)
        {
            this.Output = writer;
            this.nodeNames = nodeNames;
        }

        /* 
         * the wpf nodes are accessible from the code behind file.
         * when we generate a "code-behind" js file we have to make them
         * accessible manually, by querying the dom and creating refs
         */ 
        private void GenerateWPFReferences(string namespaceName) {

            Output.WriteLine("\n"+indents+"//Auto Generated Refs to Visual Nodes");
            Output.Write(indents+"var "+ String.Join(", ",nodeNames) +";\n" );
            Output.WriteLine(indents+ "document.addEventListener('DOMContentLoaded', function(){{" );
            foreach (var node in nodeNames)
            {
                Output.WriteLine(indents+"\t"+"{0} = document.querySelector(\"{1}[name='{0}']\");", node, namespaceName);
            }
            Output.WriteLine(indents+ "}, false);" );
            Output.WriteLine("");
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
             namespaceName = node.Name.GetText().ToString().TrimEnd();
             Output.WriteLine(String.Format("(function({0}){{", namespaceName));

             indents = new String('\t', ++Tabs);
             GenerateWPFReferences(namespaceName);

             foreach (var n in node.ChildNodesAndTokens())
             {
                 if (n.Kind == SyntaxKind.ClassDeclaration)
                 {
                     VisitClassDeclaration((ClassDeclarationSyntax)n);
                 }
             }
             //base.VisitNamespaceDeclaration(node);

             Output.WriteLine(String.Format("}})(window.{0} = window.{0} || {{}});", namespaceName));
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            indents = new String('\t', Tabs);

            className = node.Identifier.ValueText;
            Output.WriteLine(indents+node.GetLeadingTrivia());
            Output.WriteLine(indents+String.Format("(function({0}){{", className));

            bool hasConstructor = false;
            foreach (var n in node.ChildNodesAndTokens())
            {
                if (n.Kind == SyntaxKind.MethodDeclaration)
                {
                    VisitMethodDeclaration((MethodDeclarationSyntax)n);
                }
                else if (n.Kind == SyntaxKind.ConstructorDeclaration) {
                    hasConstructor = true;
                    VisitConstructorDeclaration((ConstructorDeclarationSyntax) n);
                }
                else if (n.Kind == SyntaxKind.FieldDeclaration) {
                    VisitFieldDeclaration((FieldDeclarationSyntax)n);
                }//else TODO ignore all other stuff for now
            }
            //base.VisitClassDeclaration(node);

            Output.WriteLine(indents+String.Format("}})({1}.{0} = {1}.{0} || {{}});", className, namespaceName));

            if (hasConstructor) {
                Output.WriteLine("\n"+indents + "//call constructor");
                Output.WriteLine(indents + String.Format("{1}.{0}.{0}();", className, namespaceName));
                Output.Write("\n");
            }

            Tabs--;
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            Output.Write(node.GetLeadingTrivia());
            base.VisitFieldDeclaration(node); Output.Write(node.SemicolonToken);
            Output.Write(node.GetTrailingTrivia());
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            indents = new String('\t', ++Tabs);
            Output.WriteLine(node.GetLeadingTrivia());
            var name = node.Identifier.ToString();
            Output.WriteLine(indents + "{0}.{0} = function() {{", name);
            base.VisitConstructorDeclaration(node);
            Output.WriteLine(indents + "}}", name);
            Tabs--;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            indents = new String('\t', ++Tabs);

            var name = node.Identifier.ToString();
            var _parameter = VisitParameterList(node.ParameterList);

            Output.Write(node.GetLeadingTrivia().ToString().TrimEnd() + "\n");

            //TODO distinguish between public and private
            Output.WriteLine(indents + "{2}.{0} = function({1}) {{", name, _parameter, className);

            Output.WriteLine(node.Body.GetLeadingTrivia());

            //Output.Write(node.GetLeadingTrivia());
            foreach (var n in node.ChildNodesAndTokens())
            {
                if (n.Kind == SyntaxKind.Block) {
                    base.VisitBlock((BlockSyntax)n);
                }
            }
            //Output.WriteLine(node.Body.GetTrailingTrivia());

            Output.Write(node.GetTrailingTrivia());
            //base.VisitMethodDeclaration(node);

            Output.WriteLine(indents+"};");

            Tabs--;
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            var indents = new String('\t', ++Tabs);
            //FIXME condition can contain type definitions etc..
            Output.Write(indents + "if(" + node.Condition + "){\n"); 
            base.VisitIfStatement(node);
            Output.WriteLine(indents + "}");
            Tabs--;
        }

        string VisitParameterList(ParameterListSyntax node)
        {
            var _params = new StringBuilder();
            foreach (var param in node.Parameters)
            {
                if (_params.Length != 0)
                    _params.Append(", ");
                _params.Append(param.Identifier.ToString());
            }
            return _params.ToString();
        }

        public override void VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            Output.Write(node.GetLeadingTrivia());
            VisitExpression((ExpressionSyntax)node.Expression); Output.Write(node.SemicolonToken);
            //base.VisitExpressionStatement(node);
            Output.Write(node.GetTrailingTrivia());
        }

        private void VisitExpression(ExpressionSyntax node) {
            if (node.Kind == SyntaxKind.AssignExpression)
            {
                foreach (var child in node.ChildNodesAndTokens())
                {
                    if (child.Kind != SyntaxKind.CastExpression)
                    {
                        if (child.IsToken)
                        {
                            //x = y instead of x=z
                            Output.Write(child.GetLeadingTrivia());
                            Output.Write(child);
                            Output.Write(child.GetTrailingTrivia());
                        }
                        else {
                            //no leading trivia: we want to do the indentation on our own
                            Output.Write(child);
                            Output.Write(child.GetTrailingTrivia());
                        }

                    }
                    else {
                        //don't write cast expressions, since they contain type infos
                        VisitCastExpression((CastExpressionSyntax) child);
                    }
                }
            }
            //timer.Tick += update_timeSlider
            else if(node.Kind == SyntaxKind.AddAssignExpression){
                Output.Write("//not yet supported: "+ node);
            }
            else {
                Output.Write(node);
            }
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            Output.Write(node.GetLeadingTrivia());
            base.VisitLocalDeclarationStatement(node); Output.Write(node.SemicolonToken);
            Output.Write(node.GetTrailingTrivia());
        }

        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            //FIXME this is the abbreviation of:
            /*
             child 1: SliderValue - IdentifierToken
             child 2: = (int)timelineSlider.Value - EqualsValueClause
             */
            //TODO check node.Initializer.AncestorsAndSelf().Any(x => x.Kind == SyntaxKind.PredefinedType) ) ?
            if (node.Initializer != null)
            {
                Output.Write(" " + node.Identifier + " =");
            }
            else {
                Output.Write("var " + node.Identifier);
            }
            base.VisitVariableDeclarator(node);
        }

        public override void VisitCastExpression(CastExpressionSyntax node)
        {
            base.VisitCastExpression(node);
            Output.Write(node.Expression);
        }

        public override void VisitPredefinedType(PredefinedTypeSyntax node)
        {
            if (node.Parent != null && node.Parent.Kind == SyntaxKind.CastExpression)
            {
                Output.Write(" ");
            }
            else
            {
                Output.Write("var");
            }
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            Output.Write(node.GetLeadingTrivia());
            Output.Write("return ");
            base.VisitReturnStatement(node);
            Output.WriteLine(";");
            Output.Write(node.GetTrailingTrivia());
        }

    }
}
