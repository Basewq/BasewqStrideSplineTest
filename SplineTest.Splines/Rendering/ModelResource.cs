// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Rendering;

namespace SplineTest.Rendering;

public class ModelResource
{
    public readonly List<IDisposable> Disposables = [];

    public void AddFromModel(Model model)
    {
        foreach (var mesh in model.Meshes)
        {
            foreach (var vbBinding in mesh.Draw.VertexBuffers)
            {
                Disposables.Add(vbBinding.Buffer);
            }
            Disposables.Add(mesh.Draw.IndexBuffer.Buffer);
        }
    }

    public void ReleaseResources()
    {
        foreach (var disposable in Disposables)
        {
            disposable.Dispose();
        }
        Disposables.Clear();
    }

    public static ModelResource CreateFromModel(Model model)
    {
        var modelResource = new ModelResource();
        foreach (var mesh in model.Meshes)
        {
            foreach (var vbBinding in mesh.Draw.VertexBuffers)
            {
                modelResource.Disposables.Add(vbBinding.Buffer);
            }
            modelResource.Disposables.Add(mesh.Draw.IndexBuffer.Buffer);
        }
        return modelResource;
    }
}
