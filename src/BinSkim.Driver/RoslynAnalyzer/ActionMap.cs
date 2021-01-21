// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.IL
{
    /// <summary>
    /// A map of categorized actions that can be sequentially retrieved
    /// and invoked by providing a relevant key and context object,
    /// serving as a kind of multicast delegate mechanism.
    /// </summary>
    /// <typeparam name="TContext">A context instance that is passed to the action.</typeparam>
    /// <typeparam name="TKind">A specifier that categorizes the action.</typeparam>
    internal sealed class ActionMap<TContext, TKind>
    {
        private readonly SortedList<TKind, Action<TContext>> map;

        public ActionMap()
        {
            this.map = new SortedList<TKind, Action<TContext>>();
        }

        public void Add(Action<TContext> action, ImmutableArray<TKind> kinds)
        {
            foreach (TKind kind in kinds)
            {
                this.map.TryGetValue(kind, out Action<TContext> actions);
                actions += action;
                this.map[kind] = actions;
            }
        }

        public void Invoke(TKind kind, TContext context)
        {
            if (this.map.TryGetValue(kind, out Action<TContext> actions))
            {
                actions(context);
            }
        }
    }
}
