#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace Kamgam.SandGame.PreSetup
{
    /// <summary>
    /// This class contains the package importing logic. It keeps track of what
    /// packages have been imported already and uses CrossCompileCallbacks to 
    /// call an Action once it is done.
    /// </summary>
    public static partial class PackageImporter
    {
        // ---- THIS IS WHERE YOU DECLARE WHAT PACKAGES TO IMPORT AND WHEN -------------------------------

        protected class SandGamePackage : Package
        {
            public SandGamePackage(string packagePath) : base(packagePath) { }
            public override bool IsNeeded() => true;
        }

        static List<IPackage> Packages = new List<IPackage>()
        {
            new SandGamePackage( "Assets/Kamgam/SandGamePreSetup/SandGame.unitypackage" )
        };

        // -----------------------------------------------------------------------------------------------
    }
}
#endif