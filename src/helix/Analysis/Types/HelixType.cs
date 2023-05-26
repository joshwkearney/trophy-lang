﻿using Helix.Analysis.Flow;
using Helix.Analysis.TypeChecking;
using Helix.Syntax;
using Helix.Generation;
using Helix.Generation.Syntax;
using Helix.Parsing;

namespace Helix.Analysis.Types {
    public enum UnificationKind {
        None,
        Pun,
        Convert,
        Cast
    }

    public enum PassingSemantics {
        ValueType, ContainsReferenceType, ReferenceType
    }

    public static partial class TypeExtensions {
        public static bool IsSubsetOf(this UnificationKind unify, UnificationKind other) {
            if (other == UnificationKind.Cast) {
                return unify == UnificationKind.Cast
                    || unify == UnificationKind.Convert
                    || unify == UnificationKind.Pun;
            }
            else if (other == UnificationKind.Convert) {
                return unify == UnificationKind.Convert
                    || unify == UnificationKind.Pun;
            }
            else if (other == UnificationKind.Pun) {
                return unify == UnificationKind.Pun;
            }
            else {
                return false;
            }
        }

        public static bool IsValueType(this PassingSemantics passing) {
            return passing == PassingSemantics.ValueType;
        }
    }

    public abstract record HelixType { 
        public abstract PassingSemantics GetSemantics(ITypedFrame types);

        public abstract HelixType GetMutationSupertype(ITypedFrame types);

        public abstract HelixType GetSignatureSupertype(ITypedFrame types);

        public virtual ISyntaxTree ToSyntax(TokenLocation loc) {
            return new TypeSyntaxWrapper(loc, this);
        }

        public virtual IEnumerable<HelixType> GetContainedTypes(TypeFrame frame) {
            yield return this;
        }

        public bool IsValueType(ITypedFrame types) {
            return this.GetSemantics(types) == PassingSemantics.ValueType;
        }
        
        private class TypeSyntaxWrapper : ISyntaxTree {
            private readonly HelixType type;

            public TokenLocation Location { get; }

            public IEnumerable<ISyntaxTree> Children => Enumerable.Empty<ISyntaxTree>();

            public bool IsPure => true;

            public TypeSyntaxWrapper(TokenLocation loc, HelixType type) {
                this.Location = loc;
                this.type = type;
            }

            public Option<HelixType> AsType(ITypedFrame types) => this.type;

            public ISyntaxTree CheckTypes(TypeFrame types) {
                throw new InvalidOperationException();
            }

            public ICSyntax GenerateCode(TypeFrame types, ICStatementWriter writer) {
                throw new InvalidOperationException();
            }

            public void AnalyzeFlow(FlowFrame flow) {
                throw new InvalidOperationException();
            }
        }
    }
}