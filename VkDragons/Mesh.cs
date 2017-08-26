using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;

namespace VkDragons {
    public class Mesh {
        List<Vector3> positions;
        List<Vector3> normals;
        List<Vector3> tangents;
        List<Vector3> binormals;
        List<Vector2> texCoords;
        List<uint> indices;

        public Mesh(string fileName) {
            positions = new List<Vector3>();
            normals = new List<Vector3>();
            tangents = new List<Vector3>();
            binormals = new List<Vector3>();
            texCoords = new List<Vector2>();
            indices = new List<uint>();

            Load(fileName);
            CenterAndUnitMesh();
            ComputeTangentsAndBinormals();
        }

        void Load(string fileName) {
            List<Vector3> positionsTemp = new List<Vector3>();
            List<Vector3> normalsTemp = new List<Vector3>();
            List<Vector2> texCoordsTemp = new List<Vector2>();
            List<string> faces = new List<string>();

            using (var reader = File.OpenText(fileName)) {
                while (!reader.EndOfStream) {
                    string line = reader.ReadLine();

                    if (line[0] == '#' || line.Length < 2) continue;

                    List<string> tokens = new List<string>(line.Split(' ', '\t'));

                    if (tokens.Count == 0) continue;

                    if (tokens[0] == "v") {
                        if (tokens.Count < 4) continue;

                        Vector3 pos = new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3]));
                        positionsTemp.Add(pos);
                    } else if (tokens[0] == "vn") {
                        if (tokens.Count < 4) continue;

                        Vector3 normal = new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3]));
                        normalsTemp.Add(normal);
                    } else if (tokens[0] == "vt") {
                        if (tokens.Count < 3) continue;

                        Vector2 uv = new Vector2(float.Parse(tokens[1]), float.Parse(tokens[2]));
                        texCoords.Add(uv);
                    } else if (tokens[0] == "f") {
                        if (tokens.Count < 4) continue;

                        faces.Add(tokens[1]);
                        faces.Add(tokens[2]);
                        faces.Add(tokens[3]);
                    } else {
                        continue;
                    }
                }
            }

            if (positionsTemp.Count == 0) return;

            bool hasUV = texCoordsTemp.Count > 0;
            bool hasNormals = normalsTemp.Count > 0;

            Dictionary<string, uint> indicesUsed = new Dictionary<string, uint>();
            uint maxIndex = 0;

            for (int i = 0; i < faces.Count; i++) {
                string str = faces[i];

                if (indicesUsed.ContainsKey(str)) {
                    indices.Add(indicesUsed[str]);
                    continue;
                }

                List<string> subtokens = new List<string>(str.Split('/'));
                if (subtokens.Count == 0) continue;

                uint index1 = uint.Parse(subtokens[0]);
                positions.Add(positionsTemp[(int)index1]);

                if (hasUV) {
                    uint index2 = uint.Parse(subtokens[1]);
                    texCoords.Add(texCoordsTemp[(int)index2]);
                }

                if (hasNormals) {
                    uint index3 = uint.Parse(subtokens[2]);
                    normals.Add(normalsTemp[(int)index3]);
                }

                indices.Add(maxIndex);
                indicesUsed[str] = maxIndex;
                maxIndex++;
            }
        }

        void CenterAndUnitMesh() {
            Vector3 centroid = new Vector3();
            float maxi = positions[0].X;

            foreach (var pos in positions) {
                centroid += pos;
            }

            centroid /= positions.Count;

            for (int i = 0; i < positions.Count; i++) {
                positions[i] -= centroid;
                maxi = Math.Abs(positions[i].X) > maxi ? Math.Abs(positions[i].X) : maxi;
                maxi = Math.Abs(positions[i].Y) > maxi ? Math.Abs(positions[i].Y) : maxi;
                maxi = Math.Abs(positions[i].Z) > maxi ? Math.Abs(positions[i].Z) : maxi;
            }

            maxi = maxi == 0 ? 1f : maxi;

            for (int i = 0; i < positions.Count; i++) {
                positions[i] /= maxi;
            }
        }

        void ComputeTangentsAndBinormals() {
            if (indices.Count * positions.Count * texCoords.Count == 0) return;

            for (int i = 0; i < positions.Count; i++) {
                tangents.Add(new Vector3());
                binormals.Add(new Vector3());
            }

            for (int i = 0; i < indices.Count; i += 3) {
                var v0 = positions[(int)indices[i]];
                var v1 = positions[(int)indices[i + 1]];
                var v2 = positions[(int)indices[i + 2]];

                var uv0 = texCoords[(int)indices[i]];
                var uv1 = texCoords[(int)indices[i + 1]];
                var uv2 = texCoords[(int)indices[i + 2]];

                var deltaPos1 = v1 - v0;
                var deltaPos2 = v2 - v0;
                var deltaUV1 = uv1 - uv0;
                var deltaUV2 = uv2 - uv0;

                float det = 1f / (deltaUV1.X * deltaUV2.Y - deltaUV1.Y * deltaUV2.X);
                var tangent = det * (deltaPos1 * deltaUV2.Y - deltaPos2 * deltaUV1.Y);
                var binormal = det * (deltaPos2 * deltaUV1.X - deltaPos1 * deltaUV2.X);

                tangents[(int)indices[i]] += tangent;
                tangents[(int)indices[i + 1]] += tangent;
                tangents[(int)indices[i + 2]] += tangent;

                binormals[(int)indices[i]] += binormal;
                binormals[(int)indices[i + 1]] += binormal;
                binormals[(int)indices[i + 2]] += binormal;
            }

            for (int i = 0; i < tangents.Count; i++) {
                tangents[i] = Vector3.Normalize(tangents[i] - normals[i] * Vector3.Dot(normals[i], tangents[i]));

                if (Vector3.Dot(Vector3.Cross(normals[i], tangents[i]), binormals[i]) < 0f) {
                    tangents[i] *= -1f;
                }
            }
        }
    }
}
