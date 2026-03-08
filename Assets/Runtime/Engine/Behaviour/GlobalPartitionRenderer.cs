using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Behaviour
{
    public class GlobalPartitionRenderer : Utils.Singleton<GlobalPartitionRenderer>
    {
        private readonly List<PartitionRenderer> _renderers = new();

        public void RegisterRenderer(PartitionRenderer partitionRenderer)
        {
            if (!_renderers.Contains(partitionRenderer)) _renderers.Add(partitionRenderer);
        }

        public void UnregisterRenderer(PartitionRenderer partitionRenderer)
        {
            _renderers.Remove(partitionRenderer);
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
        }

        private void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (PartitionRenderer r in _renderers)
                {
                    r.DrawForPass(pass, cam);
                }
            }
        }
    }
}