using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CsharpVoxReader.Chunks;
using Debug = UnityEngine.Debug;

namespace CsharpVoxReader
{
    public class VoxGraph
    {
        public VoxGraph(string filePathName) : this()
        {
            _reader = new VoxReader(filePathName, new VoxGraphLoader(this));
            _reader.Read();
        }

        public VoxGraph(Stream stream) : this()
        {
            _reader = new VoxReader(stream, new VoxGraphLoader(this));
            _reader.ReadFromStream();
        }

        public VoxGraph()
        {
            _models = new List<VoxModel>();
            Materials = new VoxMaterial[256];        // Material IDs are 1 based, and there are 256 of them...
            _transforms = new Dictionary<int, VoxSceneGraphNode>();
            _childByGroup = new Dictionary<int, VoxSceneGraphGroup>();
        }

        /// <summary>
        /// Shouldn't be used anymore, but still valid
        /// </summary>
        public uint[] LegacyPalette { get; private set; }

        public VoxMaterial[] Materials { get; }
        public VoxModel[] Models => _models.ToArray();
        public VoxSceneGraphGroup Root => _root;
            
        private VoxReader _reader;
        private List<VoxModel> _models;

        private Dictionary<int, VoxSceneGraphNode> _transforms;
        private VoxSceneGraphGroup _root;
        private Dictionary<int, VoxSceneGraphGroup> _childByGroup;
        
        class VoxGraphLoader : IVoxLoader
        {
            public VoxGraphLoader(VoxGraph owner)
            {
                _owner = owner;
            }
            private VoxGraph _owner;
            
            public void LoadModel(int sizeX, int sizeY, int sizeZ, byte[,,] data)
            {
                _owner.AddModel(sizeX, sizeY, sizeZ, data);
            }

            public void LoadPalette(uint[] palette)
            {
                _owner.LegacyPalette = palette;
            }

            public void SetModelCount(int count)
            {
            }

            public void SetMaterialOld(int paletteId, MaterialOld.MaterialTypes type, float weight, MaterialOld.PropertyBits property, float normalized)
            {
            }

            public void NewTransformNode(int id, int childNodeId, int layerId, Dictionary<string, object> attributes, Dictionary<string, object>[] framesAttributes)
            {
                attributes.TryGetValue("_name", out var name);
                attributes.TryGetValue("_hidden", out var hidden);
                object t = null;
                object r = null;
                if (framesAttributes.Length > 0)
                {
                    framesAttributes[0].TryGetValue("_t", out t);
                    framesAttributes[0].TryGetValue("_r", out r);
                }

                // Somehow it may happen that we have several node reference the same children, I believe this is some kind of incoherency in the MagicaVoxel internal graph, but we have to deal with it.
                // Each time the parent node (the transform) has the same properties than the other parent, so it's kind of same to ignore new parents.
                if (!_owner._transforms.ContainsKey(childNodeId))
                {
                    _owner._transforms.Add(childNodeId, new VoxSceneGraphNode(id, layerId, (string)name??"<Unnamed>", (bool?) hidden ?? false, (Vector3?) t ?? Vector3.Zero, (Matrix3x3?) r ?? Matrix3x3.Identity));
                }
                else
                {
                    //Debug.LogWarning($"Already a child node {childNodeId} targeted by {_owner._transforms[childNodeId].Id}, attempted to be replaced by targeted {id}");
                }
            }

            public void NewGroupNode(int id, Dictionary<string, byte[]> attributes, int[] childrenIds)
            {
                if (_owner._transforms.TryGetValue(id, out var node))
                {
                    var g = new VoxSceneGraphGroup(node);

                    foreach (var childrenId in childrenIds)
                    {
                        _owner._childByGroup.Add(childrenId, g);
                    }

                    // Link the group to its owner
                    if (_owner._childByGroup.TryGetValue(node.Id, out var parentGroup))
                    {
                        parentGroup._childrenGroups.Add(g);
                    }
                    else
                    {
                        if (_owner._root == null)
                        {
                            _owner._root = g;
                        }
                        else
                        {
                            Debug.LogWarning($"Couldn't find parent group {node.Id} for {g.Id}");
                        }
                    }

                    _owner._transforms.Remove(id);
                }
                else
                {
                    Debug.LogWarning($"Couldn't find transform for group node {id}");
                }
            }

            public void NewShapeNode(int id, Dictionary<string, byte[]> attributes, int[] modelIds, Dictionary<string, byte[]>[] modelsAttributes)
            {
                if (_owner._transforms.TryGetValue(id, out var node))
                {
                    var models = new List<VoxModel>();
                    foreach (var modelId in modelIds)
                    {
                        var m = _owner._models[modelId];
                        if (m != null)
                        {
                            models.Add(m);
                        }
                    }
                    var s = new VoxSceneGraphShape(node, models);

                    if (_owner._childByGroup.TryGetValue(node.Id, out var parentGroup))
                    {
                        parentGroup._childrenShapes.Add(s);
                    }
                    else
                    {
                        Debug.LogWarning($"Couldn't find parent group {node.Id} for shape {id}");
                    }
                    
                    _owner._transforms.Remove(id);
                }
                else
                {
                    Debug.LogWarning($"Couldn't find transform for shape node {id}");
                }
            }

            public void NewMaterial(int id, Dictionary<string, byte[]> attributes)
            {
                var m = new VoxMaterial();
                if (attributes.TryGetValue("_type", out var v))
                {
                    var t = Encoding.ASCII.GetString(v);
                    var e = default(VoxMaterialType);
                    switch (t)
                    {
                        case "_diffuse": e = VoxMaterialType.Diffuse;    break;
                        case "_metal":   e = VoxMaterialType.Metal;      break;
                        case "_plastic": e = VoxMaterialType.Plastic;    break;
                        case "_glass":   e = VoxMaterialType.Glass;      break;
                        case "_media":   e = VoxMaterialType.Cloud;      break;
                        case "_emit":    e = VoxMaterialType.Emission;   break;
                    }

                    m.Type = e;
                }

                if (id > 255)
                {
                    return;

                }
                
                m.Diffuse = _owner.LegacyPalette[id];

                if (attributes.TryGetValue("_weight", out v))
                {
                    var f = BytesToFloat(v);
                    m.Weight = f;
                }

                if (attributes.TryGetValue("_rough", out v))
                {
                    var f = BytesToFloat(v);
                    m.Roughness = f;
                }

                if (attributes.TryGetValue("_spec", out v))
                {
                    var f = BytesToFloat(v);
                    m.Specular = f;
                }

                if (attributes.TryGetValue("_spec_p", out v))
                {
                    var f = BytesToFloat(v);
                    m.SpecularPlastic = f;
                }

                if (attributes.TryGetValue("_ior", out v))
                {
                    var f = BytesToFloat(v);
                    m.IndexOfRefraction = f;
                }

                if (attributes.TryGetValue("_att", out v))
                {
                    var f = BytesToFloat(v);
                    m.Attenuation = f;
                }

                if (attributes.TryGetValue("_g0", out v))
                {
                    var f = BytesToFloat(v);
                    m.Phase0 = f;
                }

                if (attributes.TryGetValue("_g1", out v))
                {
                    var f = BytesToFloat(v);
                    m.Phase1 = f;
                }

                if (attributes.TryGetValue("_gw", out v))
                {
                    var f = BytesToFloat(v);
                    m.PhaseMix = f;
                }

                if (attributes.TryGetValue("_flux", out v))
                {
                    var f = BytesToFloat(v);
                    m.Flux = f;
                }

                if (attributes.TryGetValue("_ldr", out v))
                {
                    var f = BytesToFloat(v);
                    m.LowDynamicRange = f;
                }

                if (id < _owner.Materials.Length)
                {
                    _owner.Materials[id] = m;
                }
                else
                {
                    Debug.LogError("wrong Material ID");
                }
            }

            public void NewLayer(int id, Dictionary<string, byte[]> attributes)
            {
            }

            private static float BytesToFloat(byte[] values)
            {
                var t = Encoding.ASCII.GetString(values);
                var m = Regex.Match(t, @"([+-]?\d*.?\d+)");
                if (m.Success)
                {
                    float v = float.Parse(m.Groups[0].Value);
                    return v;
                }

                return default;
            }
        }

        private void AddModel(int sizeX, int sizeY, int sizeZ, byte[,,] data)
        {
            _models.Add(new VoxModel(_models.Count, new Vector3(sizeX, sizeY, sizeZ), data));
        }
    }

    public class VoxModel
    {
        public int Id { get; }
        public Vector3 Size { get; }
        public byte[,,] Data { get; }
        public VoxModel(int id, Vector3 size, byte[,,] data)
        {
            Id = id;
            Size = size;
            Data = data;
        }
    }

    public enum VoxMaterialType
    {
        None = 0,
        Diffuse = 1,
        Metal,
        Plastic,
        Glass,
        Cloud,
        Emission
    }
    public class VoxMaterial
    {
        public VoxMaterialType Type { get; internal set;}
        
        // Usually the first property of the material
        public uint Diffuse { get; internal set; }
        public float Weight { get; internal set;}
        // Roughness in the material editor (Metal, Plastic, Glass)
        public float Roughness { get; internal set;}
        // Specular in the material editor (Metal)
        public float Specular { get; internal set;}
        // Specular in the material editor (Plastic)
        public float SpecularPlastic { get; internal set;}
        // Index of Refraction (Glass)
        public float IndexOfRefraction { get; internal set;}
        // Attenuation (Glass)
        public float Attenuation { get; internal set;}
        // Phase 0 (Cloud)
        public float Phase0 { get; internal set;}
        // Phase 1 (Cloud)
        public float Phase1 { get; internal set;}
        // Phase mix (Cloud)
        public float PhaseMix { get; internal set;}
        // Radiant Flux, aka Power (Emission)
        public float Flux { get; internal set;}
        // Low Dynamic Range (Emission)
        public float LowDynamicRange { get; internal set;}
    }

    [DebuggerDisplay("Id:{Id}, Name:{Name}")]
    public class VoxSceneGraphNode
    {
        public VoxSceneGraphNode(int id, int layerId, string name, bool hidden, Vector3 translation, Matrix3x3 rotation)
        {
            Id = id;
            LayerId = layerId;
            Name = name;
            Hidden = hidden;
            Translation = translation;
            Rotation = rotation;
        }
        
        public int Id { get; }
        public int LayerId { get; }
        public string Name { get; }
        public bool Hidden { get; }
        public Vector3 Translation { get; }
        public Matrix3x3 Rotation { get; }
    }

    public class VoxSceneGraphGroup : VoxSceneGraphNode
    {
        public VoxSceneGraphGroup(VoxSceneGraphNode from) : base(from.Id, from.LayerId, from.Name, from.Hidden, from.Translation, from.Rotation)
        {
            _childrenGroups = new List<VoxSceneGraphGroup>();
            _childrenShapes = new List<VoxSceneGraphShape>();
        }

        internal List<VoxSceneGraphGroup> _childrenGroups;
        internal List<VoxSceneGraphShape> _childrenShapes;
        
        public IReadOnlyCollection<VoxSceneGraphGroup> ChildrenGroups => _childrenGroups;
        public IReadOnlyCollection<VoxSceneGraphShape> ChildrenShapes => _childrenShapes;
    }

    public class VoxSceneGraphShape : VoxSceneGraphNode
    {
        public VoxSceneGraphShape(VoxSceneGraphNode from, List<VoxModel> models) : base(from.Id, from.LayerId, from.Name, from.Hidden, from.Translation, from.Rotation)
        {
            _models = models;
        }
        private List<VoxModel> _models;
        public IReadOnlyCollection<VoxModel> Models => _models;
    }
}