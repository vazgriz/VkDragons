using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;

namespace VkDragons {
    public class Mesh {
        public List<Vector3> Positions { get; private set; }
        public List<Vector3> Normals { get; private set; }
        public List<Vector3> Tangents { get; private set; }
        public List<Vector3> Binormals { get; private set; }
        public List<Vector2> TexCoords { get; private set; }
        public List<uint> Indices { get; private set; }

        public Mesh(string fileName) {
            Positions = new List<Vector3>();
            Normals = new List<Vector3>();
            Tangents = new List<Vector3>();
            Binormals = new List<Vector3>();
            TexCoords = new List<Vector2>();
            Indices = new List<uint>();

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
                        TexCoords.Add(uv);
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
                    Indices.Add(indicesUsed[str]);
                    continue;
                }

                List<string> subtokens = new List<string>(str.Split('/'));
                if (subtokens.Count == 0) continue;

                uint index1 = uint.Parse(subtokens[0]);
                Positions.Add(positionsTemp[(int)index1]);

                if (hasUV) {
                    uint index2 = uint.Parse(subtokens[1]);
                    TexCoords.Add(texCoordsTemp[(int)index2]);
                }

                if (hasNormals) {
                    uint index3 = uint.Parse(subtokens[2]);
                    Normals.Add(normalsTemp[(int)index3]);
                }

                Indices.Add(maxIndex);
                indicesUsed[str] = maxIndex;
                maxIndex++;
            }
        }

        void CenterAndUnitMesh() {
            Vector3 centroid = new Vector3();
            float maxi = Positions[0].X;

            foreach (var pos in Positions) {
                centroid += pos;
            }

            centroid /= Positions.Count;

            for (int i = 0; i < Positions.Count; i++) {
                Positions[i] -= centroid;
                maxi = Math.Abs(Positions[i].X) > maxi ? Math.Abs(Positions[i].X) : maxi;
                maxi = Math.Abs(Positions[i].Y) > maxi ? Math.Abs(Positions[i].Y) : maxi;
                maxi = Math.Abs(Positions[i].Z) > maxi ? Math.Abs(Positions[i].Z) : maxi;
            }

            maxi = maxi == 0 ? 1f : maxi;

            for (int i = 0; i < Positions.Count; i++) {
                Positions[i] /= maxi;
            }
        }

        void ComputeTangentsAndBinormals() {
            if (Indices.Count * Positions.Count * TexCoords.Count == 0) return;

            for (int i = 0; i < Positions.Count; i++) {
                Tangents.Add(new Vector3());
                Binormals.Add(new Vector3());
            }

            for (int i = 0; i < Indices.Count; i += 3) {
                var v0 = Positions[(int)Indices[i]];
                var v1 = Positions[(int)Indices[i + 1]];
                var v2 = Positions[(int)Indices[i + 2]];

                var uv0 = TexCoords[(int)Indices[i]];
                var uv1 = TexCoords[(int)Indices[i + 1]];
                var uv2 = TexCoords[(int)Indices[i + 2]];

                var deltaPos1 = v1 - v0;
                var deltaPos2 = v2 - v0;
                var deltaUV1 = uv1 - uv0;
                var deltaUV2 = uv2 - uv0;

                float det = 1f / (deltaUV1.X * deltaUV2.Y - deltaUV1.Y * deltaUV2.X);
                var tangent = det * (deltaPos1 * deltaUV2.Y - deltaPos2 * deltaUV1.Y);
                var binormal = det * (deltaPos2 * deltaUV1.X - deltaPos1 * deltaUV2.X);

                Tangents[(int)Indices[i]] += tangent;
                Tangents[(int)Indices[i + 1]] += tangent;
                Tangents[(int)Indices[i + 2]] += tangent;

                Binormals[(int)Indices[i]] += binormal;
                Binormals[(int)Indices[i + 1]] += binormal;
                Binormals[(int)Indices[i + 2]] += binormal;
            }

            for (int i = 0; i < Tangents.Count; i++) {
                Tangents[i] = Vector3.Normalize(Tangents[i] - Normals[i] * Vector3.Dot(Normals[i], Tangents[i]));

                if (Vector3.Dot(Vector3.Cross(Normals[i], Tangents[i]), Binormals[i]) < 0f) {
                    Tangents[i] *= -1f;
                }
            }
        }
    }
}
