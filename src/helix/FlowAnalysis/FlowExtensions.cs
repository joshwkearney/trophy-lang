﻿using System;
using Helix.Analysis;
using Helix.Analysis.Lifetimes;
using Helix.Analysis.Types;
using Helix.Parsing;

namespace helix.FlowAnalysis {
    public static class FlowExtensions {
        public static LifetimeBundle GetLifetimes(this ISyntaxTree syntax, FlowFrame flow) {
            return flow.Lifetimes[syntax];
        }

        public static void SetLifetimes(this ISyntaxTree syntax, LifetimeBundle bundle, FlowFrame flow) {
            flow.Lifetimes[syntax] = bundle;
        }

        public static Dictionary<IdentifierPath, HelixType> GetMembers(this HelixType type, ITypedFrame types) {
            var dict = new Dictionary<IdentifierPath, HelixType>();

            foreach (var (memPath, memType) in GetMemberPaths(type, types)) {
                dict[memPath] = memType;
            }

            return dict;
        }

        private static IEnumerable<(IdentifierPath path, HelixType type)> GetMemberPaths(
            HelixType type,
            ITypedFrame types) {

            return GetMemberPathsHelper(new IdentifierPath(), type, types);
        }

        private static IEnumerable<(IdentifierPath path, HelixType type)> GetMemberPathsHelper(
            IdentifierPath basePath,
            HelixType type,
            ITypedFrame types) {

            yield return (basePath, type);

            if (type is not NamedType named) {
                yield break;
            }

            if (!types.Structs.TryGetValue(named.Path, out var structSig)) {
                yield break;
            }

            foreach (var mem in structSig.Members) {
                var path = basePath.Append(mem.Name);

                foreach (var subs in GetMemberPathsHelper(path, mem.Type, types)) {
                    yield return subs;
                }
            }
        }

        public static IEnumerable<Lifetime> ReduceRootSet(this FlowFrame flow, IEnumerable<Lifetime> roots) {
            var result = new List<Lifetime>();

            foreach (var root in roots) {
                if (!roots.Where(x => x != root).Any(x => flow.LifetimeGraph.DoesOutlive(x, root))) {
                    result.Add(root);
                }
            }

            return result;
        }
    }
}

