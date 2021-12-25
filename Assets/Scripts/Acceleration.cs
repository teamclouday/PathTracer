using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Bounding box
/// </summary>
public class AABB
{
    public Vector3 pMin;
    public Vector3 pMax;
    public Vector3 pExtent;

    public AABB()
    {
        pMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        pMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        pExtent = pMax - pMin;
    }

    public AABB(Vector3 min, Vector3 max)
    {
        pMin = Vector3.Min(min, max);
        pMax = Vector3.Max(min, max);
        pExtent = pMax - pMin;
    }

    public AABB(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        pMin = Vector3.Min(v0, Vector3.Min(v1, v2));
        pMax = Vector3.Max(v0, Vector3.Max(v1, v2));
        pExtent = pMax - pMin;
    }

    public void Extend(AABB volume)
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

    public static AABB Combine(AABB v1, AABB v2)
    {
        AABB result = v1.Copy();
        result.Extend(v2);
        return result;
    }

    public Vector3 Offset(Vector3 p)
    {
        Vector3 o = p - pMin;
        if (pMax.x > pMin.x) o.x /= pExtent.x;
        if (pMax.y > pMin.y) o.y /= pExtent.y;
        if (pMax.z > pMin.z) o.z /= pExtent.z;
        return o;
    }

    public float SurfaceArea()
    {
        return 2.0f * (
            pExtent.x * pExtent.y +
            pExtent.x * pExtent.z +
            pExtent.y * pExtent.z
        );
    }

    public AABB Copy()
    {
        return new AABB(pMin, pMax);
    }
}

/// <summary>
/// define BVH Type
/// </summary>
public enum BVHType
{
    Naive,
    SAH
}

/// <summary>
/// Abstract BVH class
/// </summary>
public abstract class BVH
{
    /// <summary>
    /// BVH tree node
    /// </summary>
    public class BVHNode
    {
        public AABB Bounds;
        public BVHNode LeftChild;
        public BVHNode RightChild;
        public int SplitAxis;
        public int FaceStart;
        public int FaceEnd;

        public bool IsLeaf()
        {
            return (LeftChild == null) && (RightChild == null);
        }

        public static BVHNode InitLeaf(int start, int count, AABB bounding)
        {
            BVHNode node = new BVHNode
            {
                Bounds = bounding,
                LeftChild = null,
                RightChild = null,
                SplitAxis = -1,
                FaceStart = start,
                FaceEnd = start + count
            };
            return node;
        }

        public static BVHNode InitInterior(int splitAxis, BVHNode nodeLeft, BVHNode nodeRight)
        {
            BVHNode node = new BVHNode
            {
                Bounds = AABB.Combine(nodeLeft.Bounds, nodeRight.Bounds),
                LeftChild = nodeLeft,
                RightChild = nodeRight,
                SplitAxis = splitAxis,
                FaceStart = -1,
                FaceEnd = -1
            };
            return node;
        }
    }

    /// <summary>
    /// Info for each triangle face
    /// </summary>
    public class FaceInfo
    {
        public AABB Bounds;
        public Vector3 Center;
        public int FaceIdx;
    }

    /// <summary>
    /// private tree building method
    /// should be called in constructor
    /// </summary>
    /// <param name="vertices">vertices data</param>
    /// <param name="indices">indices data</param>
    /// <param name="faceInfo">converted face info data</param>
    /// <param name="faceInfoStart">start index</param>
    /// <param name="faceInfoEnd">end index</param>
    /// <returns></returns>
    protected abstract BVHNode Build(
        List<Vector3> vertices, List<int> indices,
        List<FaceInfo> faceInfo,
        int faceInfoStart, int faceInfoEnd
    );

    /// <summary>
    /// method that converts tree into array memory
    /// </summary>
    /// <param name="indices">indices list</param>
    /// <param name="bnodes">blas node list</param>
    /// <param name="tnodes">tlas node list</param>
    /// <param name="subindices">subindices list for current tree</param>
    /// <param name="verticesOffset">vertices offset</param>
    /// <param name="materialIdx">current assigned matrial index</param>
    /// <param name="transformIdx">current assigned transform index</param>
    public void Flatten(
        ref List<int> indices, ref List<BLASNode> bnodes,
        ref List<TLASNode> tnodes, List<int> subindices,
        int verticesOffset, int materialIdx, int transformIdx
    )
    {
        int faceOffset = indices.Count / 3;
        int bnodesOffset = bnodes.Count;
        // add indices
        foreach (int faceId in OrderedFaceId)
        {
            indices.Add(subindices[faceId * 3] + verticesOffset);
            indices.Add(subindices[faceId * 3 + 1] + verticesOffset);
            indices.Add(subindices[faceId * 3 + 2] + verticesOffset);
        }
        // add BLAS nodes
        Queue<BVHNode> nodes = new Queue<BVHNode>();
        nodes.Enqueue(BVHRoot);
        while (nodes.Count > 0)
        {
            var node = nodes.Dequeue();
            bnodes.Add(new BLASNode
            {
                BoundMax = node.Bounds.pMax,
                BoundMin = node.Bounds.pMin,
                FaceStartIdx = node.FaceStart >= 0 ? node.FaceStart + faceOffset : -1,
                FaceEndIdx = node.FaceStart >= 0 ? node.FaceEnd + faceOffset : -1,
                MaterialIdx = node.FaceStart >= 0 ? materialIdx : 0,
                ChildIdx = node.FaceStart >= 0 ? -1 : nodes.Count + bnodes.Count + 1
            });
            if (node.LeftChild != null)
                nodes.Enqueue(node.LeftChild);
            if (node.RightChild != null)
                nodes.Enqueue(node.RightChild);
        }
        // add TLAS node
        tnodes.Add(new TLASNode
        {
            BoundMax = BVHRoot.Bounds.pMax,
            BoundMin = BVHRoot.Bounds.pMin,
            NodeRootIdx = bnodesOffset,
            TransformIdx = transformIdx
        });
    }

    protected List<FaceInfo> CreateFaceInfo(List<Vector3> vertices, List<int> indices)
    {
        List<FaceInfo> info = new List<FaceInfo>();
        for (int i = 0; i < indices.Count / 3; i++)
        {
            info.Add(new FaceInfo
            {
                Bounds = new AABB(
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

    public BVHNode BVHRoot;
    public List<int> OrderedFaceId = new List<int>();

    public static BVH Construct(List<Vector3> vertices, List<int> indices, BVHType type)
    {
        switch(type)
        {
            case BVHType.Naive:
                return new BVHNaive(vertices, indices);
            case BVHType.SAH:
                return new BVHSAH(vertices, indices);
            default:
                return new BVHNaive(vertices, indices);
        }
    }
}

/// <summary>
/// BVH with SAH
/// refer to https://www.pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/Bounding_Volume_Hierarchies
/// </summary>
public class BVHSAH : BVH
{
    private readonly int nBuckets = 12;

    /// <summary>
    /// Info for SAH
    /// </summary>
    public class SAHBucket
    {
        public int Count = 0;
        public AABB Bounds = new AABB();
    }

    public BVHSAH(List<Vector3> vertices, List<int> indices)
    {
        // generate face info
        var faceInfo = CreateFaceInfo(vertices, indices);
        // build tree
        BVHRoot = Build(vertices, indices, faceInfo, 0, faceInfo.Count);
    }

    protected override BVHNode Build(
        List<Vector3> vertices, List<int> indices,
        List<FaceInfo> faceInfo,
        int faceInfoStart, int faceInfoEnd
    )
    {
        // get vertices bounding
        AABB bounding = new AABB();
        for (int i = faceInfoStart; i < faceInfoEnd; i++)
            bounding.Extend(faceInfo[i].Bounds);
        int faceInfoCount = faceInfoEnd - faceInfoStart;
        // if only one face, create a leaf
        if (faceInfoCount == 1)
        {
            int idx = OrderedFaceId.Count;
            int faceIdx = faceInfo[faceInfoStart].FaceIdx;
            OrderedFaceId.Add(faceIdx);
            return BVHNode.InitLeaf(idx, faceInfoCount, bounding);
        }
        else
        {
            // get centroids bounding
            AABB centerBounding = new AABB();
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
                }
                return BVHNode.InitLeaf(idx, faceInfoCount, bounding);
            }
            else
            {
                if (faceInfoCount <= 2)
                {
                    // if only 4 faces remain, skip SAH
                    faceInfoMid = (faceInfoStart + faceInfoEnd) / 2;
                    faceInfo.Sort(
                        faceInfoStart, faceInfoCount,
                        Comparer<FaceInfo>.Create((x, y) => x.Center[dim].CompareTo(y.Center[dim]))
                    );
                }
                else
                {
                    // init SAH bucket info
                    List<SAHBucket> buckets = new List<SAHBucket>();
                    for (int i = 0; i < nBuckets; i++) buckets.Add(new SAHBucket());
                    // update buckets info
                    for (int i = faceInfoStart; i < faceInfoEnd; i++)
                    {
                        int b = (int)Mathf.Floor(nBuckets * centerBounding.Offset(faceInfo[i].Center)[dim]);
                        b = Mathf.Clamp(b, 0, nBuckets - 1);
                        buckets[b].Count += 1;
                        buckets[b].Bounds.Extend(faceInfo[i].Bounds);
                    }
                    // process counts and bounds info
                    List<int> countBelow = new List<int>() { buckets[0].Count };
                    List<int> countAbove = new List<int>() { 0 };
                    List<AABB> boundsBelow = new List<AABB>() { buckets[0].Bounds };
                    List<AABB> boundsAbove = new List<AABB>() { null };
                    for (int i = 1; i < nBuckets - 1; i++)
                    {
                        countBelow.Add(countBelow[i - 1] + buckets[i].Count);
                        boundsBelow.Add(AABB.Combine(boundsBelow[i - 1], buckets[i].Bounds));
                        countAbove.Add(0);
                        boundsAbove.Add(new AABB());
                    }
                    countAbove[nBuckets - 2] = buckets[nBuckets - 1].Count;
                    boundsAbove[nBuckets - 2] = buckets[nBuckets - 1].Bounds;
                    for (int i = nBuckets - 3; i >= 0; i--)
                    {
                        countAbove[i] = countAbove[i + 1] + buckets[i + 1].Count;
                        boundsAbove[i] = AABB.Combine(boundsAbove[i + 1], buckets[i + 1].Bounds);
                    }
                    // find bucket to split with minimum SAH cost
                    float minCost = float.MaxValue;
                    int minCostSplitBucket = -1;
                    for (int i = 0; i < nBuckets - 1; i++)
                    {
                        if (countBelow[i] == 0 || countAbove[i] == 0) continue;
                        float cost = countBelow[i] * boundsBelow[i].SurfaceArea() +
                            countAbove[i] * boundsAbove[i].SurfaceArea();
                        if (cost < minCost)
                        {
                            minCost = cost;
                            minCostSplitBucket = i;
                        }
                    }
                    //// create leaf or split primitives at selected bucket
                    float leafCost = faceInfoCount;
                    minCost = 0.5f + minCost / bounding.SurfaceArea();
                    if (faceInfoCount > 16 || minCost < leafCost)
                    {
                        var partition = faceInfo.GetRange(faceInfoStart, faceInfoCount).ToList().ToLookup(info =>
                        {
                            int b = (int)Mathf.Floor(nBuckets * centerBounding.Offset(info.Center)[dim]);
                            b = Mathf.Clamp(b, 0, nBuckets - 1);
                            return b <= minCostSplitBucket;
                        });
                        var pLeft = partition[true].ToList();
                        var pRight = partition[false].ToList();
                        faceInfoMid = pLeft.Count + faceInfoStart;
                        for (int i = faceInfoStart; i < faceInfoEnd; i++)
                            faceInfo[i] = (i < faceInfoMid) ? pLeft[i - faceInfoStart] : pRight[i - faceInfoMid];
                    }
                    else
                    {
                        int idx = OrderedFaceId.Count;
                        for (int i = faceInfoStart; i < faceInfoEnd; i++)
                        {
                            int faceIdx = faceInfo[i].FaceIdx;
                            OrderedFaceId.Add(faceIdx);
                        }
                        return BVHNode.InitLeaf(idx, faceInfoCount, bounding);
                    }
                }
                // avoid middle index error
                if (faceInfoMid == faceInfoStart) faceInfoMid = (faceInfoStart + faceInfoEnd) / 2;
                // recursively build left and right nodes
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
}

/// <summary>
/// naive BVH
/// refer to https://www.pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/Bounding_Volume_Hierarchies
/// </summary>
public class BVHNaive : BVH
{
    public BVHNaive(List<Vector3> vertices, List<int> indices)
    {
        // generate face info
        var faceInfo = CreateFaceInfo(vertices, indices);
        // build tree
        BVHRoot = Build(vertices, indices, faceInfo, 0, faceInfo.Count);
    }

    protected override BVHNode Build(
        List<Vector3> vertices, List<int> indices,
        List<FaceInfo> faceInfo,
        int faceInfoStart, int faceInfoEnd
    )
    {
        // get vertices bounding
        AABB bounding = new AABB();
        for (int i = faceInfoStart; i < faceInfoEnd; i++)
            bounding.Extend(faceInfo[i].Bounds);
        int faceInfoCount = faceInfoEnd - faceInfoStart;
        // if only one face, create a leaf
        if (faceInfoCount == 1)
        {
            int idx = OrderedFaceId.Count;
            int faceIdx = faceInfo[faceInfoStart].FaceIdx;
            OrderedFaceId.Add(faceIdx);
            return BVHNode.InitLeaf(idx, faceInfoCount, bounding);
        }
        else
        {
            // get centroids bounding
            AABB centerBounding = new AABB();
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
                }
                return BVHNode.InitLeaf(idx, faceInfoCount, bounding);
            }
            else
            {
                // find middle index
                faceInfoMid = (faceInfoStart + faceInfoEnd) / 2;
                // sort by the selected axis
                faceInfo.Sort(
                    faceInfoStart, faceInfoCount,
                    Comparer<FaceInfo>.Create((x, y) => x.Center[dim].CompareTo(y.Center[dim]))
                );
                // recursively build left and right nodes
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
}