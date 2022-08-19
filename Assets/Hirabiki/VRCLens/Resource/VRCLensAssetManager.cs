// VRCLens Copyright (c) 2020-2022 Hirabiki. All rights reserved.
// Usage of this product is subject to Terms of Use in readme_DoNotDelete.txt

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hirabiki.AV3.Works.VRCLens
{
    public class VRCLensAssetManager
    {
#if UNITY_EDITOR
        private List<string> assetPathList;
        public VRCLensAssetManager()
        {
            assetPathList = new List<string>();
        }

        public bool Add(string assetPath)
        {
            if(assetPathList.Contains(assetPath)) return false;

            assetPathList.Add(assetPath);
            return true;
        }

        public int ForceReserializeAll()
        {
            if(assetPathList == null) return -1;
            if(assetPathList.Count > 0)
            {
                AssetDatabase.ForceReserializeAssets(assetPathList);
                assetPathList.Clear();
            }
            return assetPathList.Count;
        }
#endif
    }
}
