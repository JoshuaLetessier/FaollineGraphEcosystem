using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Recipe / crafting action over a single collection: when <see cref="CollectionKey"/> contains every
    /// element in <see cref="Required"/>, consume them (remove each) and add <see cref="Reward"/>. If any
    /// required element is missing, the action makes no change. Demonstrates the consume-set→produce
    /// (fusion) pattern from the research.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Actions/Recipe", fileName = "RecipeAction")]
    public class TestRecipeAction : BaseAction
    {
        [SerializeField] private string _collectionKey;
        [SerializeField] private List<string> _required = new List<string>();
        [SerializeField] private string _reward;

        /// <summary>The collection the recipe consumes from and produces into.</summary>
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }

        /// <summary>The elements that must all be present and are consumed on success.</summary>
        public List<string> Required => _required;

        /// <summary>The element added when all required elements are present.</summary>
        public string Reward { get => _reward; set => _reward = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            foreach (var required in _required)
                if (!context.CollectionContains(_collectionKey, required))
                    return;   // missing an ingredient → no change

            foreach (var required in _required)
                context.RemoveFromCollection(_collectionKey, required);

            context.AddToCollection(_collectionKey, _reward);
        }
    }
}
