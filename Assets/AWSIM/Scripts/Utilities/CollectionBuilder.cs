using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AWSIM.Scripts.Utilities
{
    /// <summary>
    /// Collection for lists, dictionaries, etc. builders
    ///</summary>
    public static class CollectionBuilder
    {
        // Component list builder by tag
        public static List<T> CreateComponentListByTag<T>(string searchTag, Func<GameObject, T> selector)
        {
            return GameObject.FindGameObjectsWithTag(searchTag).Select(selector).ToList();
        }
    }
}
