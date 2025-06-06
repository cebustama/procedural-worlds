// https://catlikecoding.com/unity/tutorials/procedural-meshes/octasphere/
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using quaternion = Unity.Mathematics.quaternion;

namespace ProceduralWorlds.Meshes.Generators
{
    public struct GeoOctasphere : IMeshGenerator
    {
        // Four rhombus sides (4xr^2), seam (2r - 1) and polar vertices (2 per rhombus = 8)
        public int VertexCount => 4 * Resolution * Resolution + 2 * Resolution - 1 + 8;

        public int IndexCount => 6 * 4 * Resolution * Resolution;

        // One special case for the seam
        public int JobLength => 4 * Resolution + 1;

        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
        public int Resolution { get; set; }

        private struct Rhombus
        {
            public int id;
            public float3 leftCorner, rightCorner;
        }

        // Polar unwrap of the cube sphere (analogue to tetrahedron polar unwrap)
        private static Rhombus GetRhombus(int id) => id switch
        {
            // Back side
            0 => new Rhombus
            {
                id = id,
                leftCorner = back(),
                rightCorner = right()
            },
            // Right side
            1 => new Rhombus
            {
                id = id,
                leftCorner = right(),
                rightCorner = forward()
            },
            // Bottom side
            2 => new Rhombus
            {
                id = id,
                leftCorner = forward(),
                rightCorner = left()
            },
            // Top side
            _ => new Rhombus
            {
                id = id,
                leftCorner = left(),
                rightCorner = back()
            }
        };

        public void Execute<S>(int i, S streams) where S : struct, IMeshStreams
        {
            if (i == 0)
            {
                ExecutePolesAndSeam(streams);
            }
            else
            {
                ExecuteRegular(i - 1, streams);
            }
        }

        public void ExecuteRegular<S>(int i, S streams) where S : struct, IMeshStreams
        {
            int u = i / 4;
            Rhombus rhombus = GetRhombus(i - 4 * u);
            int vi = Resolution * (Resolution * rhombus.id + u + 2) + 7;
            int ti = 2 * Resolution * (Resolution * rhombus.id + u);
            bool firstColumn = (u == 0);

            int4 quad = int4(
                vi,
                firstColumn ? rhombus.id : vi - Resolution,
                firstColumn ?
                    rhombus.id == 0 ? 8 : vi - Resolution * (Resolution + u) :
                    vi - Resolution + 1,
                vi + 1
            );

            u += 1;

            var vertex = new Vertex();
            sincos(PI + PI * u / (2 * Resolution), out float sine, out vertex.position.y);
            vertex.position -= sine * rhombus.rightCorner;
            vertex.normal = vertex.position; //= columnBottomStart;
            vertex.tangent.xz = GetTangentXZ(vertex.position);
            vertex.tangent.w = -1f;
            vertex.texCoord0.x = rhombus.id * 0.25f + 0.25f;
            vertex.texCoord0.y = (float)u / (2 * Resolution);
            streams.SetVertex(vi, vertex);
            vi += 1;

            for (int v = 1; v < Resolution; v++, vi++, ti += 2)
            {
                float h = u + v;
                float3 pRight = 0f;
                sincos(PI + PI * h / (2 * Resolution), out sine, out pRight.y);
                float3 pLeft = pRight - sine * rhombus.leftCorner;
                pRight -= sine * rhombus.rightCorner;

                float3 axis = normalize(cross(pRight, pLeft));
                float angle = acos(dot(pRight, pLeft)) * (
                    v <= Resolution - u ? v / h : (Resolution - u) / (2f * Resolution - h)
                );

                vertex.normal = vertex.position = mul(
                    quaternion.AxisAngle(axis, angle), pRight
                );
                vertex.tangent.xz = GetTangentXZ(vertex.position);
                vertex.texCoord0 = GetTexCoord(vertex.position);
                streams.SetVertex(vi, vertex);
                streams.SetTriangle(ti + 0, quad.xyz);
                streams.SetTriangle(ti + 1, quad.xzw);

                quad.y = quad.z;
                quad += int4(1, 0, firstColumn && rhombus.id != 0 ? Resolution : 1, 1);
            }

            quad.z = Resolution * Resolution * rhombus.id + Resolution + u + 6;
            quad.w = u < Resolution ? quad.z + 1 : rhombus.id + 4;

            streams.SetTriangle(ti + 0, quad.xyz);
            streams.SetTriangle(ti + 1, quad.xzw);
        }

        public void ExecutePolesAndSeam<S>(S streams) where S : struct, IMeshStreams
        {
            var vertex = new Vertex();
            vertex.tangent = float4(sqrt(0.5f), 0f, sqrt(0.5f), -1f);
            vertex.texCoord0.x = 0.125f;

            for (int i = 0; i < 4; i++)
            {
                // South pole vertices
                vertex.position = vertex.normal = down();
                vertex.texCoord0.y = 0f;
                streams.SetVertex(i, vertex);
                // North pole vertices
                vertex.position = vertex.normal = up();
                vertex.texCoord0.y = 1f;
                streams.SetVertex(i + 4, vertex);
                vertex.tangent.xz = float2(-vertex.tangent.z, vertex.tangent.x);
                vertex.texCoord0.x += 0.25f;
            }

            vertex.tangent.xz = float2(1f, 0f);
            vertex.texCoord0.x = 0f;

            for (int v = 1; v < 2 * Resolution; v++)
            {
                sincos(
                    PI + PI * v / (2 * Resolution),
                    out vertex.position.z, out vertex.position.y
                );
                vertex.normal = vertex.position;
                vertex.texCoord0.y = (float)v / (2 * Resolution);
                streams.SetVertex(v + 7, vertex);
            }
        }

        private static float2 GetTangentXZ(float3 p) => normalize(float2(-p.z, p.x));

        private static float2 GetTexCoord(float3 p)
        {
            var texCoord = float2(
                atan2(p.x, p.z) / (-2f * PI) + 0.5f,
                asin(p.y) / PI + 0.5f
            );
            if (texCoord.x < 1e-6f)
            {
                texCoord.x = 1f;
            }
            return texCoord;
        }
    }
}
