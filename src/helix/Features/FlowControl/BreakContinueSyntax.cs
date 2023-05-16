﻿using Helix.Analysis.Flow;
using Helix.Analysis.TypeChecking;
using Helix.Syntax;
using Helix.Analysis.Types;
using Helix.Features.FlowControl;
using Helix.Generation;
using Helix.Generation.Syntax;
using Helix.Parsing;

namespace Helix.Parsing {
    public partial class Parser {
        public ISyntaxTree BreakStatement() {
            Token start;
            bool isBreak;

            if (this.Peek(TokenKind.BreakKeyword)) {
                start = this.Advance(TokenKind.BreakKeyword);
                isBreak = true;
            }
            else {
                start = this.Advance(TokenKind.ContinueKeyword);
                isBreak = false;
            }

            if (!this.isInLoop.Peek()) {
                throw new ParseException(
                    start.Location, 
                    "Invalid Statement", 
                    "Break and continue statements must only appear inside of loops");
            }

            return new BreakContinueSyntax(start.Location, isBreak);
        }
    }
}

namespace Helix.Features.FlowControl {
    public record BreakContinueSyntax : ISyntaxTree {
        private readonly bool isbreak;

        public TokenLocation Location { get; }

        public IEnumerable<ISyntaxTree> Children => Enumerable.Empty<ISyntaxTree>();

        public bool IsPure => false;

        public BreakContinueSyntax(TokenLocation loc, bool isbreak, 
            bool istypeChecked = false) {

            this.Location = loc;
            this.isbreak = isbreak;
        }

        public ISyntaxTree ToRValue() => this;

        public ISyntaxTree CheckTypes(TypeFrame types) {
            types.ReturnTypes[this] = PrimitiveType.Void;

            return this;
        }

        public void AnalyzeFlow(FlowFrame flow) {
            this.SetLifetimes(new LifetimeBundle(), flow);
        }

        public ICSyntax GenerateCode(FlowFrame types, ICStatementWriter writer) {
            if (this.isbreak) {
                writer.WriteStatement(new CBreak());
            }
            else {
                writer.WriteStatement(new CContinue());
            }

            return new CIntLiteral(0);
        }
    }
}
