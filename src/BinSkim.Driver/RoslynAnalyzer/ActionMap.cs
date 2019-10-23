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
    /// <typeparam name="TContext"></typeparam>
    /// <typeparam name="TKind"></typeparam>
    internal sealed class ActionMap<TContext, TKind>
    {
        private readonly SortedList<TKind, Action<TContext>> _map;

        public ActionMap()
        {
            _map = new SortedList<TKind, Action<TContext>>();
        }

        public void Add(Action<TContext> action, ImmutableArray<TKind> kinds)
        {
            foreach (var kind in kinds)
            {
                Action<TContext> actions;
                _map.TryGetValue(kind, out actions);
                actions += action;
                _map[kind] = actions;
            }
        }

        public void Invoke(TKind kind, TContext context)
        {
            Action<TContext> actions;
            if (_map.TryGetValue(kind, out actions))
                actions(context);
        }
    }
}
