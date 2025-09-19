using System;
using System.Collections;
using ProceduralMeshes;
using ProceduralMeshes.Generators;
using ProceduralMeshes.Streams;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralMesh : MonoBehaviour
{
    [SerializeField, Range(1, 1000)] private int resolution = 1;

    private MeshFilter meshFilter;

    private Coroutine coroutine;
    private Mesh theMesh;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        theMesh = new Mesh
        {
            name = "Procedural Mesh",
        };
        meshFilter.sharedMesh = theMesh;
    }

    private void OnValidate() => enabled = true;

    private void Update()
    {
        if (coroutine != null)
        {
            return;
        }

        coroutine = StartCoroutine(GenerateMesh(theMesh));
        enabled = false;
    }

    private IEnumerator GenerateMesh(Mesh mesh)
    {
        var startTime = Time.realtimeSinceStartupAsDouble;
        var meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];

        JobHandle jobHandel = MeshJob<SquareGrid, SingleStream>.ScheduleParallel(
            mesh, meshData, resolution, default
        );
        var prepFinishTime = Time.realtimeSinceStartupAsDouble;
        yield return new WaitUntil(() => jobHandel.IsCompleted);
        var jobFinishTime = Time.realtimeSinceStartupAsDouble;
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontResetBoneBounds);
        coroutine = null;
        var finishTime = Time.realtimeSinceStartupAsDouble;
        Debug.Log($"Prep Time: {(prepFinishTime - startTime) * 1000} ms, " +
                  $"Job Time: {(jobFinishTime - prepFinishTime) * 1000} ms, " +
                  $"Post Job Time: {(finishTime - jobFinishTime) * 1000} ms, " +
                  $"Time on Main Thread: {((prepFinishTime - startTime) + (finishTime - jobFinishTime)) * 1000} ms" +
                  $"Total Time: {(finishTime - startTime) * 1000} ms");
    }
}