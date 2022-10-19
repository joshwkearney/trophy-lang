﻿using Trophy.Analysis;
using Trophy.Analysis.Types;
using Trophy.Generation;
using Trophy.Features.Primitives;
using Trophy.Parsing;
using Trophy.Generation.Syntax;

namespace Trophy.Parsing {
    public partial class Parser {
        private ISyntax IntLiteral() {
            var tok = this.Advance(TokenKind.IntLiteral);
            var num = int.Parse(tok.Value);

            return new IntLiteral(tok.Location, num);
        }
    }
}

namespace Trophy.Features.Primitives {
    public record IntLiteral : ISyntax {
        public TokenLocation Location { get; }

        public int Value { get; }

        public IntLiteral(TokenLocation loc, int value) {
            this.Location = loc;
            this.Value = value;
        }

        public Option<TrophyType> AsType(ITypesRecorder names) {
            return new SingularIntType(this.Value);
        }

        public ISyntax CheckTypes(ITypesRecorder types) {
            types.SetReturnType(this, new SingularIntType(this.Value));

            return this;
        }

        public ISyntax ToRValue(ITypesRecorder types) => this;

        public ICSyntax GenerateCode(ICStatementWriter writer) {
            return new CIntLiteral(this.Value);
        }
    }
}
