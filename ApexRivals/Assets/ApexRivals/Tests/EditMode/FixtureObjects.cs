using System;
using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Tests.EditMode
{
    // Register only freshly created objects, never assets loaded from the project.
    internal sealed class FixtureObjects : IDisposable
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

        public T Own<T>(T createdObject) where T : UnityEngine.Object
        {
            _objects.Add(createdObject);
            return createdObject;
        }

        public void Dispose()
        {
            for (var index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
        }
    }
}
