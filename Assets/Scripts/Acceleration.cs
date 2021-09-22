using UnityEngine;
using System.Linq;
using System.Collections.Generic;

// defines the acceleration structure
// reference: https://github.com/GPUOpen-LibrariesAndSDKs/RadeonRays_SDK/tree/master/bvh_analyzer
// reference: https://www.pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/Bounding_Volume_Hierarchies
// reference: https://github.com/brandonpelfrey/Fast-BVH

/// <summary>
/// Bounding volume
/// </summary>
public class Bounding
{
    public Vector3 pMin;
    public Vector3 pMax;
    public Vector3 pExtent;

    public Bounding()
    {
        pMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        pMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        pExtent = pMax - pMin;
    }

    public Bounding(Vector3 min, Vector3 max)
    {
        pMin = Vector3.Min(min, max);
        pMax = Vector3.Max(min, max);
        pExtent = pMax - pMin;
    }

    public Bounding(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        pMin = Vector3.Min(v0, Vector3.Min(v1, v2));
        pMax = Vector3.Max(v0, Vector3.Max(v1, v2));
        pExtent = pMax - pMin;
    }

    public void Extend(Bounding volume)
    {
        pMin = Vector3.Min(volume.pMin, pMin);
        pMax = Vector3.Max(volume.pMax, pMax);
        pExtent = pMax - pMin;
    }

    public void Extend(Vector3 p)
    {
        pMin = Vector3.Min(p, pMin);
        pMax = Vector3.Max(p, pMax);
        pExtent = pMax - pMin;
    }

    public Vector3 Center()
    {
        return (pMin + pMax) * 0.5f;
    }

    public int MaxDimension()
    {
        int result = 0; // 0 for x, 1 for y, 2 for z
        if(pExtent.y > pExtent[result]) result = 1;
        if(pExtent.z > pExtent[result]) result = 2;
        return result;
    }

    public static Bounding Combine(Bounding v1, Bounding v2)
    {
        Bounding result = new Bounding();
        result.Extend(v1);
        result.Extend(v2);
        return result;
    }
}

/// <summary>
/// Info for each triangle face
/// </summary>
public class FaceInfo
{
    public Bounding Bounds;
    public Vector3 Center;
    public int FaceIdx;
}

/// <summary>
/// BVH tree node
/// </summary>
public class BVHNode
{
    public Bounding Bounds;
    public BVHNode LeftChild;
    public BVHNode RightChild;
    public int SplitAxis;
    public int FaceStart;
    public int FaceCount;

    public bool IsLeaf()
    {
        return (LeftChild == null) && (RightChild == null);
    }

    public static BVHNode InitLeaf(int start, int count, Bounding bounding)
    {
        BVHNode node = new BVHNode
        {
            Bounds = bounding,
            LeftChild = null,
            RightChild = null,
            SplitAxis = -1,
            FaceStart = start,
            FaceCount = count
        };
        return node;
    }

    public static BVHNode InitInterior(int splitAxis, BVHNode nodeLeft, BVHNode nodeRight)
    {
        BVHNode node = new BVHNode
        {
            Bounds = Bounding.Combine(nodeLeft.Bounds, nodeRight.Bounds),
            LeftChild = nodeLeft,
            RightChild = nodeRight,
            SplitAxis = splitAxis,
            FaceStart = -1,
            FaceCount = 0
        };
        return node;
    }
}

/// <summary>
/// BVH tree
/// </summary>
public class BVH
{
    public BVHNode BVHRoot;
    public List<int> OrderedFaceId = new List<int>();
    public List<int> OrderedIndices = new List<int>();
    
    public BVH(List<Vector3> vertices, List<int> indices)
    {
        // generate face info
        var faceInfo = CreateFaceInfo(vertices, indices);
        // build tree
        BVHRoot = Build(vertices, indices, faceInfo, 0, faceInfo.Count);
    }

    private List<FaceInfo> CreateFaceInfo(List<Vector3> vertices, List<int> indices)
    {
        List<FaceInfo> info = new List<FaceInfo>();
        for(int i = 0; i < indices.Count / 3; i++)
        {
            info.Add(new FaceInfo
            {
                Bounds = new Bounding(
                    vertices[indices[i * 3]],
                    vertices[indices[i * 3 + 1]],
                    vertices[indices[i * 3 + 2]]
                ),
                FaceIdx = i
            });
            info[i].Center = info[i].Bounds.Center();
        }
        return info;
    }

    private BVHNode Build(
        List<Vector3> vertices, List<int> indices,
        List<FaceInfo> faceInfo,
        int faceInfoStart, int faceInfoEnd
    )
    {
        // get vertices bounding
        Bounding bounding = new Bounding();
        for(int i = faceInfoStart; i < faceInfoEnd; i++)
            bounding.Extend(faceInfo[i].Bounds);
        int faceInfoCount = faceInfoEnd - faceInfoStart;
        // if only one face, create a leaf
        if(faceInfoCount == 1)
        {
            int idx = OrderedFaceId.Count;
            int faceIdx = faceInfo[faceInfoStart].FaceIdx;
            OrderedFaceId.Add(faceIdx);
            OrderedIndices.Add(indices[faceIdx * 3]);
            OrderedIndices.Add(indices[faceIdx * 3 + 1]);
            OrderedIndices.Add(indices[faceIdx * 3 + 2]);
            return BVHNode.InitLeaf(idx, faceInfoCount, bounding);
        }
        else
        {
            // get centroids bounding
            Bounding centerBounding = new Bounding();
            for (int i = faceInfoStart; i < faceInfoEnd; i++)
                centerBounding.Extend(faceInfo[i].Center);
            int dim = centerBounding.MaxDimension();
            int faceInfoMid = (faceInfoStart + faceInfoEnd) / 2;
            // if cannot further split on this axis, generate a leaf
            if (centerBounding.pMax[dim] == centerBounding.pMin[dim])
            {
                int idx = OrderedFaceId.Count;
                for (int i = faceInfoStart; i < faceInfoEnd; i++)
                {
                    int faceIdx = faceInfo[i].FaceIdx;
                    OrderedFaceId.Add(faceIdx);
                    OrderedIndices.Add(indices[faceIdx * 3]);
                    OrderedIndices.Add(indices[faceIdx * 3 + 1]);
                    OrderedIndices.Add(indices[faceIdx * 3 + 2]);
                }
                return BVHNode.InitLeaf(idx, faceInfoCount, bounding);
            }
            else
            {
                // reorder faces
                //var orderedInfo = faceInfo.Skip(faceInfoStart).Take(faceInfoCount)
                //    .OrderBy(x => x.Center[dim]).ToList();
                //for (int i = 0; i < orderedInfo.Count; i++)
                //    faceInfo[i + faceInfoStart] = orderedInfo[i];
                faceInfo.Sort(
                    faceInfoStart, faceInfoCount,
                    Comparer<FaceInfo>.Create((x, y) => x.Center[dim].CompareTo(y.Center[dim]))
                );
                var leftChild = Build(vertices, indices, faceInfo, faceInfoStart, faceInfoMid);
                var rightChild = Build(vertices, indices, faceInfo, faceInfoMid, faceInfoEnd);
                return BVHNode.InitInterior(
                    dim,
                    leftChild,
                    rightChild
                );
            }
        }
    }

    public void Flatten(List<NodeInfo> result, List<int> materialInfo)
    {
        Queue<BVHNode> nodes = new Queue<BVHNode>();
        nodes.Enqueue(BVHRoot);
        while(nodes.Count > 0)
        {
            var node = nodes.Dequeue();
            result.Add(new NodeInfo
            {
                BoundMax = node.Bounds.pMax,
                BoundMin = node.Bounds.pMin,
                FaceStartIdx = node.FaceStart,
                FaceCount = node.FaceCount,
                MaterialIdx = node.FaceStart >= 0 ? materialInfo[OrderedFaceId[node.FaceStart]] : 0,
                ChildIdx = node.FaceStart >= 0 ? -1 : nodes.Count + result.Count + 1
            });
            if (node.LeftChild != null)
                nodes.Enqueue(node.LeftChild);
            if (node.RightChild != null)
                nodes.Enqueue(node.RightChild);
        }
    }
}
