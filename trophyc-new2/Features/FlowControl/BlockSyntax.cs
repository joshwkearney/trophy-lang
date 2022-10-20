﻿using Trophy.Analysis;
using Trophy.Analysis.Types;
using Trophy.Generation;
using Trophy.Features.FlowControl;
using Trophy.Parsing;
using Trophy.Generation.Syntax;
using Trophy.Features.Primitives;

namespace Trophy.Parsing {
    public partial class Parser {
        private int blockCounter = 0;

        private ISyntaxTree Block(BlockBuilder block) {
            var start = this.Advance(TokenKind.OpenBrace);
            var stats = new List<ISyntaxTree>();

            this.scope = this.scope.Append("$block_" + this.blockCounter++);

            while (!this.Peek(TokenKind.CloseBrace)) {
                stats.Add(this.Statement(block));
            }

            this.scope = this.scope.Pop();

            var end = this.Advance(TokenKind.CloseBrace);
            var loc = start.Location.Span(end.Location);

            if (stats.Any()) {
                return stats.Last();
            }
            else {
                return new VoidLiteral(loc);
            }
        }
    }
}

namespace Trophy.Features.FlowControl {
    public record BlockSyntax : ISyntaxTree {
        private static int idCounter = 0;

        private readonly IReadOnlyList<ISyntaxTree> statements;
        private readonly int id;
        private readonly bool isTypeChecked;

        public TokenLocation Location { get; }

        public IEnumerable<ISyntaxTree> Children => this.statements;

        public BlockSyntax(TokenLocation location, IReadOnlyList<ISyntaxTree> statements,
                   bool isTypeChecked = false) {
            this.Location = location;
            this.statements = statements;
            this.id = idCounter++;
            this.isTypeChecked = isTypeChecked;
        }

        public ISyntaxTree CheckTypes(SyntaxFrame types) {
            var stats = this.statements.Select(x => x.CheckTypes(types)).ToArray();
            var result = new BlockSyntax(this.Location, stats, true);
            var returnType = stats
                .LastOrNone()
                .Select(x => types.ReturnTypes[x])
                .OrElse(() => PrimitiveType.Void);

            types.ReturnTypes[result] = returnType;

            return result;
        }

        public ISyntaxTree ToRValue(SyntaxFrame types) {
            if (!this.isTypeChecked) {
                throw TypeCheckingErrors.RValueRequired(this.Location);
            }

            return this;
        }

        public ICSyntax GenerateCode(ICStatementWriter writer) {
            if (!this.isTypeChecked) {
                throw new InvalidOperationException();
            }

            if (this.statements.Any()) {
                foreach (var stat in this.statements.SkipLast(1)) {
                    stat.GenerateCode(writer);
                }

                return this.statements.Last().GenerateCode(writer);
            }
            else {
                return new CIntLiteral(0);
            }
        }
    }
}
