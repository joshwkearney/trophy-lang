﻿using Trophy.Analysis;
using Trophy.Analysis.Types;
using Trophy.Generation;
using Trophy.Generation.CSyntax;
using Trophy.Features.Primitives;
using Trophy.Parsing;
using Trophy.Generation.Syntax;

namespace Trophy.Parsing {
    public partial class Parser {
        private ISyntax UnaryExpression() {
            var hasOperator = this.Peek(TokenKind.Subtract)
                || this.Peek(TokenKind.Add)
                || this.Peek(TokenKind.Not)
                || this.Peek(TokenKind.Multiply);

            if (hasOperator) {
                var tokOp = this.Advance();
                var first = this.SuffixExpression();
                var loc = tokOp.Location.Span(first.Location);
                var op = UnaryOperatorKind.Not;

                if (tokOp.Kind == TokenKind.Multiply) {
                    return new DereferenceSyntax(loc, first);
                }
                else if (tokOp.Kind == TokenKind.Add) {
                    op = UnaryOperatorKind.Plus;
                }
                else if (tokOp.Kind == TokenKind.Subtract) {
                    op = UnaryOperatorKind.Minus;
                }

                return new UnaryParseSyntax(loc, op, first);
            }

            return this.SuffixExpression();
        }
    }
}

namespace Trophy.Features.Primitives {
    public enum UnaryOperatorKind {
        Not, Plus, Minus
    }

    public record UnaryParseSyntax : ISyntax {
        private readonly UnaryOperatorKind op;
        private readonly ISyntax arg;

        public TokenLocation Location { get; }

        public UnaryParseSyntax(TokenLocation location, UnaryOperatorKind op, ISyntax arg) {
            this.Location = location;
            this.op = op;
            this.arg = arg;
        }

        public Option<TrophyType> TryInterpret(INamesRecorder names) {
            return Option.None;
        }

        public ISyntax CheckTypes(ITypesRecorder types) {
            if (this.op == UnaryOperatorKind.Plus || this.op == UnaryOperatorKind.Minus) {
                var left = new IntLiteral(this.Location, 0);

                var op = this.op == UnaryOperatorKind.Plus 
                    ? BinaryOperationKind.Add 
                    : BinaryOperationKind.Subtract;

                var result = new BinarySyntax(this.Location, left, this.arg, op);

                return result.CheckTypes(types);
            }
            else if (this.op == UnaryOperatorKind.Not) {
                var result = new BinarySyntax(
                    this.Location, 
                    new BoolLiteral(this.Location, true), 
                    this.arg, 
                    BinaryOperationKind.Xor);

                return result.CheckTypes(types);
            }
            else {
                throw new Exception("Unexpected unary operator kind");
            }
        }

        public Option<ISyntax> ToRValue(ITypesRecorder types) {
            throw new InvalidOperationException();
        }

        public Option<ISyntax> ToLValue(ITypesRecorder types) {
            throw new InvalidOperationException();
        }

        public ICSyntax GenerateCode(ICStatementWriter writer) {
            throw new InvalidOperationException();
        }
    }
}