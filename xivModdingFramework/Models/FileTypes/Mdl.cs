﻿// xivModdingFramework
// Copyright © 2018 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.Wpf.SharpDX.Core;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.Enums;
using xivModdingFramework.Mods;
using xivModdingFramework.Resources;
using xivModdingFramework.SqPack.FileTypes;
using BoundingBox = xivModdingFramework.Models.DataContainers.BoundingBox;

namespace xivModdingFramework.Models.FileTypes
{
    public class Mdl
    {
        private const string MdlExtension = ".mdl";
        private readonly DirectoryInfo _gameDirectory;
        private readonly DirectoryInfo _modListDirectory;
        private readonly XivDataFile _dataFile;

        /// <summary>
        /// This value represents the amount to multiply the model data
        /// </summary>
        /// <remarks>
        /// It was determined that the values being used were too small so they are multiplied by 10 (default)
        /// </remarks>
        private const int ModelMultiplier = 10;

        public Mdl(DirectoryInfo gameDirectory, XivDataFile dataFile)
        {
            _gameDirectory = gameDirectory;
            _modListDirectory = new DirectoryInfo(Path.Combine(gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));

            _dataFile = dataFile;
        }

        public byte[] MDLRawData { get; set; }

        /// <summary>
        /// Gets the MDL Data given a model and race
        /// </summary>
        /// <param name="itemModel">The Item model</param>
        /// <param name="xivRace">The race for which to get the data</param>
        /// <param name="secondaryModel">The secondary model info if needed</param>
        /// <returns>An XivMdl structure containing all mdl data.</returns>
        public XivMdl GetMdlData(IItemModel itemModel, XivRace xivRace, XivModelInfo secondaryModel = null, string mdlStringPath = null)
        {
            var index = new Index(_gameDirectory);
            var dat = new Dat(_gameDirectory);

            var itemType = ItemType.GetItemType(itemModel);

            var mdlPath = GetMdlPath(itemModel, xivRace, itemType, secondaryModel, mdlStringPath);

            var offset = index.GetDataOffset(HashGenerator.GetHash(mdlPath.Folder), HashGenerator.GetHash(mdlPath.File),
                _dataFile);

            if (offset == 0)
            {
                throw new Exception($"Could not find offest for {mdlPath.Folder}/{mdlPath.File}");
            }

            var mdlData = dat.GetType3Data(offset, _dataFile);

            var xivMdl = new XivMdl {MdlPath = mdlPath};

            using (var br = new BinaryReader(new MemoryStream(mdlData.Data)))
            {
                // We skip the Vertex Data Structures for now
                // This is done so that we can get the correct number of meshes per LoD first
                br.BaseStream.Seek(64 + 136 * mdlData.MeshCount + 4, SeekOrigin.Begin);

                var mdlPathData = new MdlPathData()
                {
                    PathCount     = br.ReadInt32(),
                    PathBlockSize = br.ReadInt32(),
                    AttributeList = new List<string>(),
                    BoneList      = new List<string>(),
                    MaterialList  = new List<string>(),
                    ShapeList     = new List<string>(),
                    ExtraPathList = new List<string>()
                };

                // Get the entire path string block to parse later
                // This will be done when we obtain the path counts for each type
                var pathBlock = br.ReadBytes(mdlPathData.PathBlockSize);

                var mdlModelData = new MdlModelData()
                {
                    Unknown0            = br.ReadInt32(),
                    MeshCount           = br.ReadInt16(),
                    AttributeCount      = br.ReadInt16(),
                    MeshPartCount       = br.ReadInt16(),
                    MaterialCount       = br.ReadInt16(),
                    BoneCount           = br.ReadInt16(),
                    BoneListCount       = br.ReadInt16(),
                    ShapeCount          = br.ReadInt16(),
                    ShapeDataCount      = br.ReadInt16(),
                    ShapeIndexCount     = br.ReadInt16(),
                    Unknown1            = br.ReadInt16(),
                    Unknown2            = br.ReadInt16(),
                    Unknown3            = br.ReadInt16(),
                    Unknown4            = br.ReadInt16(),
                    Unknown5            = br.ReadInt16(),
                    Unknown6            = br.ReadInt16(),
                    Unknown7            = br.ReadInt16(),
                    Unknown8            = br.ReadInt16(), // Used for transform count with furniture
                    Unknown9            = br.ReadInt16(),
                    Unknown10a          = br.ReadByte(),
                    Unknown10b          = br.ReadByte(),
                    Unknown11           = br.ReadInt16(),
                    Unknown12           = br.ReadInt16(),
                    Unknown13           = br.ReadInt16(),
                    Unknown14           = br.ReadInt16(),
                    Unknown15           = br.ReadInt16(),
                    Unknown16           = br.ReadInt16(),
                    Unknown17           = br.ReadInt16()
                };

                // Finished reading all MdlModelData
                // Adding to xivMdl
                xivMdl.ModelData = mdlModelData;

                // Now that we have the path counts wee can parse the path strings
                using (var br1 = new BinaryReader(new MemoryStream(pathBlock)))
                {
                    // Attribute Paths
                    for (var i = 0; i < mdlModelData.AttributeCount; i++)
                    {
                        // Because we don't know the length of the string, we read the data until we reach a 0 value
                        // That 0 value is the space between strings
                        byte a;
                        var atrName = new List<byte>();
                        while ((a = br1.ReadByte()) != 0)
                        {
                            atrName.Add(a);
                        }

                        // Read the string from the byte array and remove null terminators
                        var atr = Encoding.ASCII.GetString(atrName.ToArray()).Replace("\0", "");

                        // Add the attribute to the list
                        mdlPathData.AttributeList.Add(atr);
                    }

                    // Bone Paths
                    for (var i = 0; i < mdlModelData.BoneCount; i++)
                    {
                        byte a;
                        var boneName = new List<byte>();
                        while ((a = br1.ReadByte()) != 0)
                        {
                            boneName.Add(a);
                        }

                        var bone = Encoding.ASCII.GetString(boneName.ToArray()).Replace("\0", "");

                        mdlPathData.BoneList.Add(bone);
                    }

                    // Material Paths
                    for (var i = 0; i < mdlModelData.MaterialCount; i++)
                    {
                        byte a;
                        var materialName = new List<byte>();
                        while ((a = br1.ReadByte()) != 0)
                        {
                            materialName.Add(a);
                        }

                        var mat = Encoding.ASCII.GetString(materialName.ToArray()).Replace("\0", "");

                        mdlPathData.MaterialList.Add(mat);
                    }

                    // Shape Paths
                    for (var i = 0; i < mdlModelData.ShapeCount; i++)
                    {
                        byte a;
                        var shapeName = new List<byte>();
                        while ((a = br1.ReadByte()) != 0)
                        {
                            shapeName.Add(a);
                        }

                        var shp = Encoding.ASCII.GetString(shapeName.ToArray()).Replace("\0", "");

                        mdlPathData.ShapeList.Add(shp);
                    }

                    var remainingPathData = mdlPathData.PathBlockSize - br1.BaseStream.Position;
                    if (remainingPathData > 2)
                    {
                        while (remainingPathData != 0)
                        {
                            byte a;
                            var extraName = new List<byte>();
                            while ((a = br1.ReadByte()) != 0)
                            {
                                extraName.Add(a);
                                remainingPathData--;
                            }

                            remainingPathData--;

                            if (extraName.Count > 0)
                            {
                                var extra = Encoding.ASCII.GetString(extraName.ToArray()).Replace("\0", "");

                                mdlPathData.ExtraPathList.Add(extra);
                            }
                        }

                    }
                }

                // Finished reading all Path Data
                // Adding to xivMdl
                xivMdl.PathData = mdlPathData;

                // Currently Unknown Data
                var unkData0 = new UnknownData0
                {
                    Unknown = br.ReadBytes(mdlModelData.Unknown2 * 32)
                };

                // Finished reading all UnknownData0
                // Adding to xivMdl
                xivMdl.UnkData0 = unkData0;

                var totalLoDMeshes = 0;

                // We add each LoD to the list
                // Note: There is always 3 LoD
                xivMdl.LoDList = new List<LevelOfDetail>();
                for (var i = 0; i < 3; i++)
                {
                    var lod = new LevelOfDetail
                    {
                        MeshOffset       = br.ReadInt16(),
                        MeshCount        = br.ReadInt16(),
                        Unknown0         = br.ReadInt32(),
                        Unknown1         = br.ReadInt32(),
                        Unknown2         = br.ReadInt32(),
                        Unknown3         = br.ReadInt32(),
                        Unknown4         = br.ReadInt32(),
                        Unknown5         = br.ReadInt32(),
                        Unknown6         = br.ReadInt32(),
                        IndexDataStart   = br.ReadInt32(),
                        Unknown7         = br.ReadInt32(),
                        Unknown8         = br.ReadInt32(),
                        VertexDataSize   = br.ReadInt32(),
                        IndexDataSize    = br.ReadInt32(),
                        VertexDataOffset = br.ReadInt32(),
                        IndexDataOffset  = br.ReadInt32(),
                        MeshDataList     = new List<MeshData>()
                    };
                    // Finished reading LoD

                    totalLoDMeshes += lod.MeshCount;

                    // if LoD0 shows no mesh, add one (This is rare, but happens on company chest for example)
                    if (i == 0 && lod.MeshCount == 0)
                    {
                        lod.MeshCount = 1;
                    }

                    //Adding to xivMdl
                    xivMdl.LoDList.Add(lod);
                }

                if (totalLoDMeshes < mdlModelData.MeshCount)
                {
                    xivMdl.ExtraLoDList = new List<LevelOfDetail>();

                    for (var i = 0; i < mdlModelData.Unknown10a; i++)
                    {
                        var lod = new LevelOfDetail
                        {
                            MeshOffset = br.ReadInt16(),
                            MeshCount = br.ReadInt16(),
                            Unknown0 = br.ReadInt32(),
                            Unknown1 = br.ReadInt32(),
                            Unknown2 = br.ReadInt32(),
                            Unknown3 = br.ReadInt32(),
                            Unknown4 = br.ReadInt32(),
                            Unknown5 = br.ReadInt32(),
                            Unknown6 = br.ReadInt32(),
                            IndexDataStart = br.ReadInt32(),
                            Unknown7 = br.ReadInt32(),
                            Unknown8 = br.ReadInt32(),
                            VertexDataSize = br.ReadInt32(),
                            IndexDataSize = br.ReadInt32(),
                            VertexDataOffset = br.ReadInt32(),
                            IndexDataOffset = br.ReadInt32(),
                            MeshDataList = new List<MeshData>()
                        };

                        xivMdl.ExtraLoDList.Add(lod);
                    }
                }

                // Now that we have the LoD data, we can go back and read the Vertex Data Structures
                // First we save our current position
                var savePosition = br.BaseStream.Position;

                var loDStructPos = 68;
                // for each mesh in each lod
                for (var i = 0; i < xivMdl.LoDList.Count; i++)
                {
                    for (var j = 0; j < xivMdl.LoDList[i].MeshCount; j++)
                    {
                        xivMdl.LoDList[i].MeshDataList.Add(new MeshData());
                        xivMdl.LoDList[i].MeshDataList[j].VertexDataStructList = new List<VertexDataStruct>();

                        // LoD Index * Vertex Data Structure size + Header
                        
                        br.BaseStream.Seek(j * 136 + loDStructPos, SeekOrigin.Begin);

                        // If the first byte is 255, we reached the end of the Vertex Data Structs
                        var dataBlockNum = br.ReadByte();
                        while (dataBlockNum != 255)
                        {
                            var vertexDataStruct = new VertexDataStruct
                            {
                                DataBlock  = dataBlockNum,
                                DataOffset = br.ReadByte(),
                                DataType   = VertexTypeDictionary[br.ReadByte()],
                                DataUsage  = VertexUsageDictionary[br.ReadByte()]
                            };

                            xivMdl.LoDList[i].MeshDataList[j].VertexDataStructList.Add(vertexDataStruct);

                            // padding between Vertex Data Structs
                            br.ReadBytes(4);

                            dataBlockNum = br.ReadByte();
                        }
                    }

                    // This will add another entry if there is a discrepancy in mesh counts for the LoD
                    if (xivMdl.LoDList[i].MeshCount == 0 && loDStructPos < (136 * xivMdl.ModelData.MeshCount))
                    {
                        xivMdl.LoDList[i].MeshDataList.Add(new MeshData());
                        xivMdl.LoDList[i].MeshDataList[0].VertexDataStructList = new List<VertexDataStruct>();

                        // LoD Index * Vertex Data Structure size + Header

                        br.BaseStream.Seek(loDStructPos, SeekOrigin.Begin);

                        // If the first byte is 255, we reached the end of the Vertex Data Structs
                        var dataBlockNum = br.ReadByte();
                        while (dataBlockNum != 255)
                        {
                            var vertexDataStruct = new VertexDataStruct
                            {
                                DataBlock = dataBlockNum,
                                DataOffset = br.ReadByte(),
                                DataType = VertexTypeDictionary[br.ReadByte()],
                                DataUsage = VertexUsageDictionary[br.ReadByte()]
                            };

                            xivMdl.LoDList[i].MeshDataList[0].VertexDataStructList.Add(vertexDataStruct);

                            // padding between Vertex Data Structs
                            br.ReadBytes(4);

                            dataBlockNum = br.ReadByte();
                        }
                    }

                    loDStructPos += 136 * xivMdl.LoDList[i].MeshCount;
                }

                // Now that we finished reading the Vertex Data Structures, we can go back to our saved position
                br.BaseStream.Seek(savePosition, SeekOrigin.Begin);

                // Mesh Data Information
                var meshNum = 0;
                foreach (var lod in xivMdl.LoDList)
                {
                    for (var i = 0; i < lod.MeshCount; i++)
                    {
                        var meshDataInfo = new MeshDataInfo
                        {
                            VertexCount          = br.ReadInt32(),
                            IndexCount           = br.ReadInt32(),
                            MaterialIndex        = br.ReadInt16(),
                            MeshPartIndex        = br.ReadInt16(),
                            MeshPartCount        = br.ReadInt16(),
                            BoneListIndex        = br.ReadInt16(),
                            IndexDataOffset      = br.ReadInt32(),
                            VertexDataOffset0    = br.ReadInt32(),
                            VertexDataOffset1    = br.ReadInt32(),
                            VertexDataOffset2    = br.ReadInt32(),
                            VertexDataEntrySize0 = br.ReadByte(),
                            VertexDataEntrySize1 = br.ReadByte(),
                            VertexDataEntrySize2 = br.ReadByte(),
                            VertexDataBlockCount = br.ReadByte()
                        };

                        lod.MeshDataList[i].MeshInfo = meshDataInfo;

                        var materialString = xivMdl.PathData.MaterialList[meshDataInfo.MaterialIndex];
                        var typeChar = materialString[4].ToString() + materialString[9].ToString();

                        if (typeChar.Equals("cb"))
                        {
                            lod.MeshDataList[i].IsBody = true;
                        }

                        meshNum++;
                    }

                    // This will add another entry if there is a discrepancy in mesh counts for the LoD
                    if (lod.MeshCount == 0 && meshNum < xivMdl.ModelData.MeshCount)
                    {
                        var meshDataInfo = new MeshDataInfo
                        {
                            VertexCount = br.ReadInt32(),
                            IndexCount = br.ReadInt32(),
                            MaterialIndex = br.ReadInt16(),
                            MeshPartIndex = br.ReadInt16(),
                            MeshPartCount = br.ReadInt16(),
                            BoneListIndex = br.ReadInt16(),
                            IndexDataOffset = br.ReadInt32(),
                            VertexDataOffset0 = br.ReadInt32(),
                            VertexDataOffset1 = br.ReadInt32(),
                            VertexDataOffset2 = br.ReadInt32(),
                            VertexDataEntrySize0 = br.ReadByte(),
                            VertexDataEntrySize1 = br.ReadByte(),
                            VertexDataEntrySize2 = br.ReadByte(),
                            VertexDataBlockCount = br.ReadByte()
                        };

                        lod.MeshDataList[0].MeshInfo = meshDataInfo;

                        meshNum++;
                    }
                }

                // Adds any extra mesh data 
                if (meshNum < mdlModelData.MeshCount)
                {
                    var missingMeshCount = mdlModelData.MeshCount - meshNum;

                    xivMdl.ExtraMeshData = new List<MeshData>();

                    for (var i = 0; i < missingMeshCount; i++)
                    {
                        var meshDataInfo = new MeshDataInfo
                        {
                            VertexCount = br.ReadInt32(),
                            IndexCount = br.ReadInt32(),
                            MaterialIndex = br.ReadInt16(),
                            MeshPartIndex = br.ReadInt16(),
                            MeshPartCount = br.ReadInt16(),
                            BoneListIndex = br.ReadInt16(),
                            IndexDataOffset = br.ReadInt32(),
                            VertexDataOffset0 = br.ReadInt32(),
                            VertexDataOffset1 = br.ReadInt32(),
                            VertexDataOffset2 = br.ReadInt32(),
                            VertexDataEntrySize0 = br.ReadByte(),
                            VertexDataEntrySize1 = br.ReadByte(),
                            VertexDataEntrySize2 = br.ReadByte(),
                            VertexDataBlockCount = br.ReadByte()
                        };

                        xivMdl.ExtraMeshData.Add(new MeshData{ MeshInfo = meshDataInfo });
                    }
                }

                // Data block for attributes offset paths
                var attributeDataBlock = new AttributeDataBlock
                {
                    AttributePathOffsetList = new List<int>(xivMdl.ModelData.AttributeCount)
                };

                for (var i = 0; i < xivMdl.ModelData.AttributeCount; i++)
                {
                    attributeDataBlock.AttributePathOffsetList.Add(br.ReadInt32());
                }

                xivMdl.AttrDataBlock = attributeDataBlock;

                // Unknown data block
                // This is commented out to allow housing items to display, the data does not exist for housing items
                // more investigation needed as to what this data is
                var unkData1 = new UnknownData1
                {
                    //Unknown = br.ReadBytes(xivMdl.ModelData.Unknown3 * 20)
                };
                xivMdl.UnkData1 = unkData1;

                // Mesh Parts
                foreach (var lod in xivMdl.LoDList)
                {
                    foreach (var meshData in lod.MeshDataList)
                    {
                        meshData.MeshPartList = new List<MeshPart>();

                        for (var i = 0; i < meshData.MeshInfo.MeshPartCount; i++)
                        {
                            var meshPart = new MeshPart
                            {
                                IndexOffset     = br.ReadInt32(),
                                IndexCount      = br.ReadInt32(),
                                AttributeIndex  = br.ReadInt32(),
                                BoneStartOffset = br.ReadInt16(),
                                BoneCount       = br.ReadInt16()
                            };

                            meshData.MeshPartList.Add(meshPart);
                        }
                    }
                }

                // Unknown data block
                var unkData2 = new UnknownData2
                {
                    Unknown = br.ReadBytes(xivMdl.ModelData.Unknown9 * 12)
                };
                xivMdl.UnkData2 = unkData2;

                // Data block for materials
                // Currently unknown usage
                var matDataBlock = new MaterialDataBlock
                {
                    MaterialPathOffsetList = new List<int>(xivMdl.ModelData.MaterialCount)
                };

                for (var i = 0; i < xivMdl.ModelData.MaterialCount; i++)
                {
                    matDataBlock.MaterialPathOffsetList.Add(br.ReadInt32());
                }

                xivMdl.MatDataBlock = matDataBlock;

                // Data block for bones
                // Currently unknown usage
                var boneDataBlock = new BoneDataBlock
                {
                    BonePathOffsetList = new List<int>(xivMdl.ModelData.BoneCount)
                };

                for (var i = 0; i < xivMdl.ModelData.BoneCount; i++)
                {
                    boneDataBlock.BonePathOffsetList.Add(br.ReadInt32());
                }

                xivMdl.BonDataBlock = boneDataBlock;

                // Bone Lists
                xivMdl.BoneIndexMeshList = new List<BoneIndexMesh>();
                for (var i = 0; i < xivMdl.ModelData.BoneListCount; i++)
                {
                    var boneIndexMesh = new BoneIndexMesh
                    {
                        BoneIndices = new List<short>(64)
                    };

                    for (var j = 0; j < 64; j++)
                    {
                        boneIndexMesh.BoneIndices.Add(br.ReadInt16());
                    }

                    boneIndexMesh.BoneIndexCount = br.ReadInt32();

                    xivMdl.BoneIndexMeshList.Add(boneIndexMesh);
                }

                var shapeDataLists = new ShapeData
                {
                    ShapeInfoList     = new List<ShapeData.ShapeInfo>(),
                    ShapeDataInfoList = new List<ShapeData.ShapeIndexInfo>(),
                    ShapeDataList     = new List<ShapeData.ShapeEntryData>()
                };

                var totalPartCount = 0;
                // Shape Info

                for (var i = 0; i < xivMdl.ModelData.ShapeCount; i++)
                {
                    var shapeInfo = new ShapeData.ShapeInfo
                    {
                        ShapePathOffset = br.ReadInt32(),
                        ShapePath = xivMdl.PathData.ShapeList[i],
                        ShapeIndexParts = new List<ShapeData.ShapeIndexPart>()
                    };

                    var dataInfoIndexList = new List<short>();
                    for (var j = 0; j < xivMdl.LoDList.Count; j++)
                    {
                        dataInfoIndexList.Add(br.ReadInt16());
                    }

                    var infoPartCountList = new List<short>();
                    for (var j = 0; j < xivMdl.LoDList.Count; j++)
                    {
                        infoPartCountList.Add(br.ReadInt16());
                    }

                    for (var j = 0; j < xivMdl.LoDList.Count; j++)
                    {
                        var shapeIndexPart = new ShapeData.ShapeIndexPart
                        {
                            DataInfoIndex = dataInfoIndexList[j],
                            PartCount = infoPartCountList[j]
                        };
                        shapeInfo.ShapeIndexParts.Add(shapeIndexPart);
                        totalPartCount += shapeIndexPart.PartCount;
                    }

                    shapeDataLists.ShapeInfoList.Add(shapeInfo);
                }

                // Shape Index Info
                for (var i = 0; i < xivMdl.ModelData.ShapeDataCount; i++)
                {
                    var shapeIndexInfo = new ShapeData.ShapeIndexInfo
                    {
                        IndexDataOffset = br.ReadInt32(),
                        IndexCount      = br.ReadInt32(),
                        DataIndexOffset = br.ReadInt32()
                    };

                    shapeDataLists.ShapeDataInfoList.Add(shapeIndexInfo);
                }

                // Shape data
                for (var i = 0; i < xivMdl.ModelData.ShapeIndexCount; i++)
                {
                    var shapeData = new ShapeData.ShapeEntryData
                    {
                        ReferenceIndexOffset = br.ReadInt16(),
                        ShapeIndex            = br.ReadInt16()
                    };

                    shapeDataLists.ShapeDataList.Add(shapeData);
                }

                xivMdl.MeshShapeData = shapeDataLists;

                // Sets the boolean flag if the model has shape data
                xivMdl.HasShapeData = xivMdl.ModelData.ShapeCount > 0;

                // Bone index for Parts
                var boneIndexPart = new BoneIndexPart
                {
                    BoneIndexCount = br.ReadInt32(),
                    BoneIndexList  = new List<short>()
                };

                for (var i = 0; i < boneIndexPart.BoneIndexCount / 2; i++)
                {
                    boneIndexPart.BoneIndexList.Add(br.ReadInt16());
                }

                xivMdl.BoneIndexPart = boneIndexPart;

                // Padding
                xivMdl.PaddingSize = br.ReadByte();
                xivMdl.PaddedBytes = br.ReadBytes(xivMdl.PaddingSize);

                // Bounding box
                var boundingBox = new BoundingBox
                {
                    PointList = new List<Vector4>()
                };

                for (var i = 0; i < 8; i++)
                {
                    boundingBox.PointList.Add(new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                }

                xivMdl.BoundBox = boundingBox;

                // Bone Transform Data
                xivMdl.BoneTransformDataList = new List<BoneTransformData>();

                var transformCount = xivMdl.ModelData.BoneCount;

                if (itemType == XivItemType.furniture)
                {
                    transformCount = xivMdl.ModelData.Unknown8;
                }

                for (var i = 0; i < transformCount; i++)
                {
                    var boneTransformData = new BoneTransformData
                    {
                        Transform0 = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                        Transform1 = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle())
                    };

                    xivMdl.BoneTransformDataList.Add(boneTransformData);
                }

                var lodNum = 0;
                var totalMeshNum = 0;
                foreach (var lod in xivMdl.LoDList)
                {
                    if(lod.MeshCount == 0) continue;

                    var meshDataList = lod.MeshDataList;

                    if (lod.MeshOffset != totalMeshNum)
                    {
                        meshDataList = xivMdl.LoDList[lodNum + 1].MeshDataList;
                    }

                    foreach (var meshData in meshDataList)
                    {
                        var vertexData = new VertexData
                        {
                            Positions = new Vector3Collection(),
                            BoneWeights = new List<float[]>(),
                            BoneIndices = new List<byte[]>(),
                            Normals = new Vector3Collection(),
                            BiNormals = new Vector3Collection(),
                            BiNormalHandedness = new List<byte>(),
                            Tangents = new Vector3Collection(),
                            Colors = new List<Color>(),
                            TextureCoordinates0 = new Vector2Collection(),
                            TextureCoordinates1 = new Vector2Collection(),
                            Indices = new IntCollection()
                        };

                        #region Positions
                        // Get the Vertex Data Structure for positions
                        var posDataStruct = (from vertexDataStruct in meshData.VertexDataStructList
                                             where vertexDataStruct.DataUsage == VertexUsageType.Position
                                             select vertexDataStruct).FirstOrDefault();

                        int vertexDataOffset;
                        int vertexDataSize;

                        if (posDataStruct != null)
                        {
                            // Determine which data block the position data is in
                            // This always seems to be in the first data block
                            switch (posDataStruct.DataBlock)
                            {
                                case 0:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset0;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize0;
                                    break;
                                case 1:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset1;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize1;

                                    break;
                                default:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset2;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize2;
                                    break;
                            }

                            for (var i = 0; i < meshData.MeshInfo.VertexCount; i++)
                            {
                                // Get the offset for the position data for each vertex
                                var positionOffset = lod.VertexDataOffset + vertexDataOffset + posDataStruct.DataOffset + vertexDataSize * i;

                                // Go to the Data Block
                                br.BaseStream.Seek(positionOffset, SeekOrigin.Begin);

                                Vector3 positionVector;
                                // Position data is either stored in half-floats or singles
                                if (posDataStruct.DataType == VertexDataType.Half4)
                                {
                                    var x = new SharpDX.Half(br.ReadUInt16());
                                    var y = new SharpDX.Half(br.ReadUInt16());
                                    var z = new SharpDX.Half(br.ReadUInt16());
                                    var w = new SharpDX.Half(br.ReadUInt16());

                                    positionVector = new Vector3(x, y, z);
                                }
                                else
                                {
                                    var x = br.ReadSingle();
                                    var y = br.ReadSingle();
                                    var z = br.ReadSingle();

                                    positionVector = new Vector3(x, y, z);
                                }
                                vertexData.Positions.Add(positionVector);
                            }
                        }

                        #endregion


                        #region BoneWeights

                        // Get the Vertex Data Structure for bone weights
                        var bwDataStruct = (from vertexDataStruct in meshData.VertexDataStructList
                            where vertexDataStruct.DataUsage == VertexUsageType.BoneWeight
                            select vertexDataStruct).FirstOrDefault();

                        if (bwDataStruct != null)
                        {
                            // Determine which data block the bone weight data is in
                            // This always seems to be in the first data block
                            switch (bwDataStruct.DataBlock)
                            {
                                case 0:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset0;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize0;
                                    break;
                                case 1:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset1;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize1;

                                    break;
                                default:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset2;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize2;
                                    break;
                            }

                            // There is always one set of bone weights per vertex
                            for (var i = 0; i < meshData.MeshInfo.VertexCount; i++)
                            {
                                var bwOffset = lod.VertexDataOffset + vertexDataOffset + bwDataStruct.DataOffset + vertexDataSize * i;

                                br.BaseStream.Seek(bwOffset, SeekOrigin.Begin);

                                var bw0 = br.ReadByte() / 255f;
                                var bw1 = br.ReadByte() / 255f;
                                var bw2 = br.ReadByte() / 255f;
                                var bw3 = br.ReadByte() / 255f;

                                vertexData.BoneWeights.Add(new[] { bw0, bw1, bw2, bw3 });
                            }
                        }


                        #endregion


                        #region BoneIndices

                        // Get the Vertex Data Structure for bone indices
                        var biDataStruct = (from vertexDataStruct in meshData.VertexDataStructList
                            where vertexDataStruct.DataUsage == VertexUsageType.BoneIndex
                            select vertexDataStruct).FirstOrDefault();

                        if (biDataStruct != null)
                        {
                            // Determine which data block the bone index data is in
                            // This always seems to be in the first data block
                            switch (biDataStruct.DataBlock)
                            {
                                case 0:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset0;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize0;
                                    break;
                                case 1:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset1;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize1;

                                    break;
                                default:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset2;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize2;
                                    break;
                            }

                            // There is always one set of bone indices per vertex
                            for (var i = 0; i < meshData.MeshInfo.VertexCount; i++)
                            {
                                var biOffset = lod.VertexDataOffset + vertexDataOffset + biDataStruct.DataOffset + vertexDataSize * i;

                                br.BaseStream.Seek(biOffset, SeekOrigin.Begin);

                                var bi0 = br.ReadByte();
                                var bi1 = br.ReadByte();
                                var bi2 = br.ReadByte();
                                var bi3 = br.ReadByte();

                                vertexData.BoneIndices.Add(new[] { bi0, bi1, bi2, bi3 });
                            }
                        }

                        #endregion


                        #region Normals

                        // Get the Vertex Data Structure for Normals
                        var normDataStruct = (from vertexDataStruct in meshData.VertexDataStructList
                            where vertexDataStruct.DataUsage == VertexUsageType.Normal
                            select vertexDataStruct).FirstOrDefault();

                        if (normDataStruct != null)
                        {
                            // Determine which data block the normal data is in
                            // This always seems to be in the second data block
                            switch (normDataStruct.DataBlock)
                            {
                                case 0:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset0;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize0;
                                    break;
                                case 1:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset1;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize1;

                                    break;
                                default:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset2;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize2;
                                    break;
                            }

                            // There is always one set of normals per vertex
                            for (var i = 0; i < meshData.MeshInfo.VertexCount; i++)
                            {
                                var normOffset = lod.VertexDataOffset + vertexDataOffset + normDataStruct.DataOffset + vertexDataSize * i;

                                br.BaseStream.Seek(normOffset, SeekOrigin.Begin);

                                Vector3 normalVector;
                                // Normal data is either stored in half-floats or singles
                                if (normDataStruct.DataType == VertexDataType.Half4)
                                {
                                    var x = new SharpDX.Half(br.ReadUInt16());
                                    var y = new SharpDX.Half(br.ReadUInt16());
                                    var z = new SharpDX.Half(br.ReadUInt16());
                                    var w = new SharpDX.Half(br.ReadUInt16());

                                    normalVector = new Vector3(x, y, z);
                                }
                                else
                                {
                                    var x = br.ReadSingle();
                                    var y = br.ReadSingle();
                                    var z = br.ReadSingle();

                                    normalVector = new Vector3(x, y, z);
                                }

                                vertexData.Normals.Add(normalVector);
                            }
                        }

                        #endregion


                        #region BiNormals

                        // Get the Vertex Data Structure for BiNormals
                        var biNormDataStruct = (from vertexDataStruct in meshData.VertexDataStructList
                            where vertexDataStruct.DataUsage == VertexUsageType.Binormal
                            select vertexDataStruct).FirstOrDefault();

                        if (biNormDataStruct != null)
                        {
                            // Determine which data block the binormal data is in
                            // This always seems to be in the second data block
                            switch (biNormDataStruct.DataBlock)
                            {
                                case 0:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset0;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize0;
                                    break;
                                case 1:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset1;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize1;

                                    break;
                                default:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset2;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize2;
                                    break;
                            }

                            // There is always one set of biNormals per vertex
                            for (var i = 0; i < meshData.MeshInfo.VertexCount; i++)
                            {
                                var biNormOffset = lod.VertexDataOffset + vertexDataOffset + biNormDataStruct.DataOffset + vertexDataSize * i;

                                br.BaseStream.Seek(biNormOffset, SeekOrigin.Begin);

                                var x = br.ReadByte() * 2 / 255f - 1f;
                                var y = br.ReadByte() * 2 / 255f - 1f;
                                var z = br.ReadByte() * 2 / 255f - 1f;
                                var w = br.ReadByte();

                                vertexData.BiNormals.Add(new Vector3(x, y, z));
                                vertexData.BiNormalHandedness.Add(w);
                            }
                        }

                        #endregion

                        #region Tangents

                        // Get the Vertex Data Structure for Tangents
                        var tangentDataStruct = (from vertexDataStruct in meshData.VertexDataStructList
                                                where vertexDataStruct.DataUsage == VertexUsageType.Tangent
                                                select vertexDataStruct).FirstOrDefault();

                        if (tangentDataStruct != null)
                        {
                            // Determine which data block the tangent data is in
                            // This always seems to be in the second data block
                            switch (tangentDataStruct.DataBlock)
                            {
                                case 0:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset0;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize0;
                                    break;
                                case 1:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset1;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize1;

                                    break;
                                default:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset2;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize2;
                                    break;
                            }

                            // There is one set of tangents per vertex
                            for (var i = 0; i < meshData.MeshInfo.VertexCount; i++)
                            {
                                var tangentOffset = lod.VertexDataOffset + vertexDataOffset + tangentDataStruct.DataOffset + vertexDataSize * i;

                                br.BaseStream.Seek(tangentOffset, SeekOrigin.Begin);

                                var x = br.ReadByte() * 2 / 255f - 1f;
                                var y = br.ReadByte() * 2 / 255f - 1f;
                                var z = br.ReadByte() * 2 / 255f - 1f;
                                var w = br.ReadByte();

                                vertexData.Tangents.Add(new Vector3(x, y, z));
                                //vertexData.TangentHandedness.Add(w);
                            }
                        }

                        #endregion


                        #region VertexColor

                        // Get the Vertex Data Structure for colors
                        var colorDataStruct = (from vertexDataStruct in meshData.VertexDataStructList
                            where vertexDataStruct.DataUsage == VertexUsageType.Color
                            select vertexDataStruct).FirstOrDefault();

                        if (colorDataStruct != null)
                        {
                            // Determine which data block the color data is in
                            // This always seems to be in the second data block
                            switch (colorDataStruct.DataBlock)
                            {
                                case 0:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset0;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize0;
                                    break;
                                case 1:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset1;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize1;

                                    break;
                                default:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset2;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize2;
                                    break;
                            }

                            // There is always one set of colors per vertex
                            for (var i = 0; i < meshData.MeshInfo.VertexCount; i++)
                            {
                                var colorOffset = lod.VertexDataOffset + vertexDataOffset + colorDataStruct.DataOffset + vertexDataSize * i;

                                br.BaseStream.Seek(colorOffset, SeekOrigin.Begin);

                                var a = br.ReadByte();
                                var r = br.ReadByte();
                                var g = br.ReadByte();
                                var b = br.ReadByte();

                                vertexData.Colors.Add(new Color(r, g, b, a));
                            }
                        }

                        #endregion


                        #region TextureCoordinates

                        // Get the Vertex Data Structure for texture coordinates
                        var tcDataStruct = (from vertexDataStruct in meshData.VertexDataStructList
                            where vertexDataStruct.DataUsage == VertexUsageType.TextureCoordinate
                            select vertexDataStruct).FirstOrDefault();

                        if (tcDataStruct != null)
                        {
                            // Determine which data block the texture coordinate data is in
                            // This always seems to be in the second data block
                            switch (tcDataStruct.DataBlock)
                            {
                                case 0:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset0;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize0;
                                    break;
                                case 1:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset1;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize1;
                                    break;
                                default:
                                    vertexDataOffset = meshData.MeshInfo.VertexDataOffset2;
                                    vertexDataSize = meshData.MeshInfo.VertexDataEntrySize2;
                                    break;
                            }

                            // There is always one set of texture coordinates per vertex
                            for (var i = 0; i < meshData.MeshInfo.VertexCount; i++)
                            {
                                var tcOffset = lod.VertexDataOffset + vertexDataOffset + tcDataStruct.DataOffset + vertexDataSize * i;

                                br.BaseStream.Seek(tcOffset, SeekOrigin.Begin);

                                Vector2 tcVector1;
                                Vector2 tcVector2;
                                // Normal data is either stored in half-floats or singles
                                if (tcDataStruct.DataType == VertexDataType.Half4)
                                {
                                    var x = new SharpDX.Half(br.ReadUInt16());
                                    var y = new SharpDX.Half(br.ReadUInt16());
                                    var x1 = new SharpDX.Half(br.ReadUInt16());
                                    var y1 = new SharpDX.Half(br.ReadUInt16());

                                    tcVector1 = new Vector2(x, y);
                                    tcVector2 = new Vector2(x1, y1);


                                    vertexData.TextureCoordinates0.Add(tcVector1);
                                    vertexData.TextureCoordinates1.Add(tcVector2);
                                }
                                else if (tcDataStruct.DataType == VertexDataType.Half2)
                                {
                                    var x = new SharpDX.Half(br.ReadUInt16());
                                    var y = new SharpDX.Half(br.ReadUInt16());

                                    tcVector1 = new Vector2(x, y);

                                    vertexData.TextureCoordinates0.Add(tcVector1);
                                }
                                else if (tcDataStruct.DataType == VertexDataType.Float2)
                                {
                                    var x = br.ReadSingle();
                                    var y = br.ReadSingle();

                                    tcVector1 = new Vector2(x, y);
                                    vertexData.TextureCoordinates0.Add(tcVector1);
                                }
                                else if(tcDataStruct.DataType == VertexDataType.Float4)
                                {
                                    var x = br.ReadSingle();
                                    var y = br.ReadSingle();
                                    var x1 = br.ReadSingle();
                                    var y1 = br.ReadSingle();

                                    tcVector1 = new Vector2(x, y);
                                    tcVector2 = new Vector2(x1, y1);


                                    vertexData.TextureCoordinates0.Add(tcVector1);
                                    vertexData.TextureCoordinates1.Add(tcVector2);
                                }

                            }
                        }

                        #endregion

                        #region Indices

                        var indexOffset = lod.IndexDataOffset + meshData.MeshInfo.IndexDataOffset * 2;

                        br.BaseStream.Seek(indexOffset, SeekOrigin.Begin);

                        for (var i = 0; i < meshData.MeshInfo.IndexCount; i++)
                        {
                            vertexData.Indices.Add(br.ReadInt16());
                        }

                        #endregion

                        meshData.VertexData = vertexData;
                        totalMeshNum++;
                    }

                    #region MeshShape

                    // If the model contains Shape Data, parse the data for each mesh
                    if (xivMdl.HasShapeData)
                    {
                        //Dictionary containing <index data offset, mesh number>
                        var indexMeshNum = new Dictionary<int, int>();

                        var shapeData = xivMdl.MeshShapeData.ShapeDataList;

                        // Get the index data offsets in each mesh
                        for (var i = 0; i < lod.MeshCount; i++)
                        {
                            var indexDataOffset = lod.MeshDataList[i].MeshInfo.IndexDataOffset;

                            indexMeshNum.Add(indexDataOffset, i);
                        }

                        for (var i = 0; i < lod.MeshCount; i++)
                        {
                            Debug.WriteLine($"=== Mesh {i} ===");
                            var referencePositionsDictionary = new Dictionary<int, Vector3>();
                            var meshShapePositionsDictionary = new SortedDictionary<int, Vector3>();
                            var shapeIndexOffsetDictionary = new Dictionary<int, Dictionary<short, short>>();

                            // Shape info list
                            var shapeInfoList = xivMdl.MeshShapeData.ShapeInfoList;

                            // Number of shape info in each mesh
                            var perMeshCount = xivMdl.ModelData.ShapeCount;

                            for (var j = 0; j < perMeshCount; j++)
                            {
                                var shapeInfo = shapeInfoList[j];

                                var indexPart = shapeInfo.ShapeIndexParts[lodNum];

                                // The part count
                                var infoPartCount = indexPart.PartCount;

                                for (var k = 0; k < infoPartCount; k++)
                                {
                                    // Gets the data info for the part
                                    var shapeDataInfo = xivMdl.MeshShapeData.ShapeDataInfoList[indexPart.DataInfoIndex + k];

                                    // The offset in the shape data 
                                    var indexDataOffset = shapeDataInfo.IndexDataOffset;

                                    var indexMeshLocation = 0;

                                    // Determine which mesh the info belongs to
                                    if (indexMeshNum.ContainsKey(indexDataOffset))
                                    {
                                        indexMeshLocation = indexMeshNum[indexDataOffset];

                                        // Move to the next part if it is not the current mesh
                                        if (indexMeshLocation != i)
                                        {
                                            continue;
                                        }
                                    }

                                    // Get the mesh data
                                    var mesh = lod.MeshDataList[indexMeshLocation];

                                    // Get the shape data for the current mesh
                                    var shapeDataForMesh = shapeData.GetRange(shapeDataInfo.DataIndexOffset, shapeDataInfo.IndexCount);

                                    // Fill shape data dictionaries
                                    foreach (var data in shapeDataForMesh)
                                    {
                                        if (!shapeIndexOffsetDictionary.ContainsKey(shapeDataInfo.DataIndexOffset))
                                        {
                                            shapeIndexOffsetDictionary.Add(shapeDataInfo.DataIndexOffset, new Dictionary<short, short>{{ data.ReferenceIndexOffset, data.ShapeIndex }});
                                        }
                                        else
                                        {
                                            shapeIndexOffsetDictionary[shapeDataInfo.DataIndexOffset].Add(data.ReferenceIndexOffset, data.ShapeIndex);
                                        }

                                        if (data.ReferenceIndexOffset >= mesh.VertexData.Indices.Count)
                                        {
                                            throw new Exception($"Reference Index is larger than the index count. Refrence Index: {data.ReferenceIndexOffset}  Index Count: {mesh.VertexData.Indices.Count}");
                                        }

                                        // Gets the index to which the data is referencing
                                        var referenceIndex = mesh.VertexData.Indices[data.ReferenceIndexOffset];

                                        if (!referencePositionsDictionary.ContainsKey(data.ReferenceIndexOffset))
                                        {
                                            referencePositionsDictionary.Add(data.ReferenceIndexOffset, mesh.VertexData.Positions[referenceIndex]);
                                        }

                                        if (data.ShapeIndex >= mesh.VertexData.Positions.Count)
                                        {
                                            throw new Exception($"Shape Index is larger than the positions count. Shape Index: {data.ShapeIndex}  Positions Count: {mesh.VertexData.Positions.Count}");
                                        }

                                        if (!meshShapePositionsDictionary.ContainsKey(data.ShapeIndex))
                                        {
                                            meshShapePositionsDictionary.Add(data.ShapeIndex, mesh.VertexData.Positions[data.ShapeIndex]);
                                        }
                                    }

                                    mesh.ShapeIndexOffsetDictionary = shapeIndexOffsetDictionary;
                                    mesh.ReferencePositionsDictionary = referencePositionsDictionary;
                                    mesh.ShapePositionsDictionary = new Dictionary<int, Vector3>(meshShapePositionsDictionary);
                                }
                            }
                        }
                    }

                    lodNum++;

                    #endregion
                }
            }

            return xivMdl;
        }

        /// <summary>
        /// Import a given model
        /// </summary>
        /// <param name="item">The current item being imported</param>
        /// <param name="xivMdl">The model data for the given item</param>
        /// <param name="daeLocation">The location of the dae file to import</param>
        /// <param name="advImportSettings">The advanced import settings if any</param>
        /// <param name="source">The source/application that is writing to the dat.</param>
        /// <returns>A dictionary containing any warnings encountered during import.</returns>
        public Dictionary<string, string> ImportModel(IItemModel item, XivMdl xivMdl, DirectoryInfo daeLocation,
            Dictionary<string, ModelImportSettings> advImportSettings, string source, bool rawDataOnly = false)
        {
            if (!File.Exists(daeLocation.FullName))
            {
                throw new IOException("The file provided for import does not exist");
            }

            if (!Path.GetExtension(daeLocation.FullName).ToLower().Equals(".dae"))
            {
                throw new FormatException("The file provided is not a collada .dae file");
            }

            var isHousingItem = item.Category.Equals(XivStrings.Housing);

            var meshShapeDictionary = new Dictionary<int, int>();

            // A dictonary containing any warnings raised by the import in the format <Warning Title, Warning Message>
            var warningsDictionary = new Dictionary<string, string>();

            var dae = new Dae(_gameDirectory, _dataFile);

            // We only use the highest quality LoD for importing which is LoD 0
            var lod0 = xivMdl.LoDList[0];

            var numMeshes = lod0.MeshCount;

            var meshDataDictionary = new Dictionary<int, ColladaData>();

            var meshPartDataDictionary = dae.ReadColladaFile(xivMdl, daeLocation);

            int textureCoordinateStride = 3;
            for (var i = 0; i < numMeshes; i++)
            {
                var meshPartDict = meshPartDataDictionary[i];

                if (meshPartDict.ContainsKey(i))
                {
                    textureCoordinateStride = meshPartDict[0].TextureCoordinateStride;
                }

                meshDataDictionary.Add(i, new ColladaData
                {
                    TextureCoordinateStride = textureCoordinateStride
                });
            }
         
            // Check for missing data and throw exception if no data is found
            for (var i = 0; i < meshPartDataDictionary.Count; i++)
            {
                var partDataDict = meshPartDataDictionary[i];

                foreach (var partData in partDataDict)
                {
                    if (partData.Value.TextureCoordinates0.Count < 1)
                    {
                        throw new Exception($"Missing Texture Coordinates at Mesh: {i}  Part: {partData.Key}");
                    }

                    if (isHousingItem) continue; // Housing items do not have bones

                    if (partData.Value.BoneWeights.Count < 1)
                    {
                        throw new Exception($"Missing Bone Weights at Mesh: {i}  Part: {partData.Key}");
                    }

                    if (partData.Value.BoneIndices.Count < 1)
                    {
                        throw new Exception($"Missing Bone Indices at Mesh: {i}  Part: {partData.Key}");
                    }

                }
            }

            for (var i = 0; i < meshPartDataDictionary.Count; i++)
            {
                var partDataDict = meshPartDataDictionary[i];

                var bInList = new List<int>();

                var partNum = 0;
                var positionMax = 0;
                var normalMax = 0;
                var texCoord0Max = 0;
                var texCoord1Max = 0;
                var biNormalMax = 0;

                if (partDataDict.Count > 0)
                {
                    for (var j = 0; j < partDataDict.Count; j++)
                    {
                        // Check if the part number exists in the imported data
                        // and if it does not, add it to the parts dictionary with 0 for index count
                        while (!partDataDict.ContainsKey(partNum))
                        {
                            meshDataDictionary[i].PartsDictionary.Add(partNum, 0);
                            partNum++;
                        }

                        // Consolidate all data into one Collada Data per mesh
                        meshDataDictionary[i].Positions.AddRange(partDataDict[partNum].Positions);
                        meshDataDictionary[i].Normals.AddRange(partDataDict[partNum].Normals);
                        meshDataDictionary[i].TextureCoordinates0.AddRange(partDataDict[partNum].TextureCoordinates0);
                        meshDataDictionary[i].TextureCoordinates1.AddRange(partDataDict[partNum].TextureCoordinates1);
                        meshDataDictionary[i].Tangents.AddRange(partDataDict[partNum].Tangents);
                        meshDataDictionary[i].BiNormals.AddRange(partDataDict[partNum].BiNormals);

                        // Consolidate all index data into one Collada Data per mesh
                        for (var k = 0; k < partDataDict[partNum].PositionIndices.Count; k++)
                        {
                            meshDataDictionary[i].Indices.Add(partDataDict[partNum].PositionIndices[k] + positionMax);
                            meshDataDictionary[i].Indices.Add(partDataDict[partNum].NormalIndices[k] + normalMax);
                            meshDataDictionary[i].Indices.Add(partDataDict[partNum].TextureCoordinate0Indices[k] + texCoord0Max);

                            if (partDataDict[partNum].TextureCoordinates1.Count > 0)
                            {
                                meshDataDictionary[i].Indices.Add(partDataDict[partNum].TextureCoordinate1Indices[k] + texCoord1Max);
                            }

                            if (partDataDict[partNum].BiNormals.Count > 0)
                            {
                                meshDataDictionary[i].Indices.Add(partDataDict[partNum].BiNormalIndices[k] + biNormalMax);
                            }
                        }

                        // Get the largest index for each data point
                        positionMax += partDataDict[partNum].PositionIndices.Max() + 1;
                        normalMax += partDataDict[partNum].NormalIndices.Max() + 1;
                        texCoord0Max += partDataDict[partNum].TextureCoordinate0Indices.Max() + 1;

                        if (partDataDict[partNum].TextureCoordinates1.Count > 0)
                        {
                            texCoord1Max += partDataDict[partNum].TextureCoordinate1Indices.Max() + 1;
                        }

                        if (partDataDict[partNum].BiNormals.Count > 0)
                        {
                            biNormalMax += partDataDict[partNum].BiNormalIndices.Max() + 1;
                        }

                        // Add the part number and index count for each part in the mesh
                        meshDataDictionary[i].PartsDictionary.Add(partNum, partDataDict[partNum].Indices.Count / partDataDict[partNum].IndexStride);

                        if (!isHousingItem) // housing items do not have bones
                        {
                            // Consolidate all weight data into one Collada Data per mesh
                            meshDataDictionary[i].BoneWeights.AddRange(partDataDict[partNum].BoneWeights);
                            meshDataDictionary[i].Vcounts.AddRange(partDataDict[partNum].Vcounts);

                            // Consolidate all bone index data into one Collada Data per mesh
                            if (j > 0)
                            {
                                var lastIndex = bInList.Max() + 1;

                                for (var a = 0; a < partDataDict[partNum].BoneIndices.Count; a += 2)
                                {
                                    meshDataDictionary[i].BoneIndices.Add(partDataDict[partNum].BoneIndices[a]);
                                    meshDataDictionary[i].BoneIndices.Add(partDataDict[partNum].BoneIndices[a + 1] + lastIndex);
                                    bInList.Add(partDataDict[partNum].BoneIndices[a + 1] + lastIndex);
                                }
                            }
                            else
                            {
                                for (var a = 0; a < partDataDict[partNum].BoneIndices.Count; a += 2)
                                {
                                    meshDataDictionary[i].BoneIndices.Add(partDataDict[partNum].BoneIndices[a]);
                                    meshDataDictionary[i].BoneIndices.Add(partDataDict[partNum].BoneIndices[a + 1]);
                                    bInList.Add(partDataDict[partNum].BoneIndices[a + 1]);
                                }
                            }
                        }

                        partNum++;
                    }
                }
                // There are no parts in the mesh
                else
                {
                    meshDataDictionary[i].PartsDictionary.Add(partNum, 0);
                }


            }

            var colladaMeshDataList = new List<ColladaMeshData>();

            var meshNum = 0;
            foreach (var colladaData in meshDataDictionary.Values)
            {
                // Make the data into collections of vectors
                var positionCollection = new Vector3Collection();
                var texCoord0Collection = new Vector2Collection();
                var texCoord1Collection = new Vector2Collection();
                var normalsCollection = new Vector3Collection();
                var tangentsCollection = new Vector3Collection();
                var biNormalsCollection = new Vector3Collection();
                var indexCollection = new IntCollection();
                var boneIndexCollection = new List<byte[]>();
                var boneWeightCollection = new List<byte[]>();
                var boneStringList = new List<string>();


                var nPositionCollection = new Vector3Collection();
                var nTexCoord0Collection = new Vector2Collection();
                var nTexCoord1Collection = new Vector2Collection();
                var nNormalsCollection = new Vector3Collection();
                var nTangentsCollection = new Vector3Collection();
                var nBiNormalsCollection = new Vector3Collection();
                var nBoneIndexCollection = new List<byte[]>();
                var nBoneWeightCollection = new List<byte[]>();

                for (var i = 0; i < colladaData.Positions.Count; i += 3)
                {
                    positionCollection.Add(new Vector3((colladaData.Positions[i] / ModelMultiplier),
                        (colladaData.Positions[i + 1] / ModelMultiplier),
                        (colladaData.Positions[i + 2] / ModelMultiplier)));
                }

                for (var i = 0; i < colladaData.Normals.Count; i += 3)
                {
                    normalsCollection.Add(new Vector3(colladaData.Normals[i], colladaData.Normals[i + 1],
                        colladaData.Normals[i + 2]));
                }

                if (colladaData.BiNormals.Count > 0)
                {
                    for (var i = 0; i < colladaData.BiNormals.Count; i += 3)
                    {
                        biNormalsCollection.Add(new Vector3(colladaData.BiNormals[i], colladaData.BiNormals[i + 1],
                            colladaData.BiNormals[i + 2]));
                    }
                }

                if (colladaData.Tangents.Count > 0)
                {
                    for (var i = 0; i < colladaData.Tangents.Count; i += 3)
                    {
                        tangentsCollection.Add(new Vector3(colladaData.Tangents[i], colladaData.Tangents[i + 1],
                            colladaData.Tangents[i + 2]));
                    }
                }

                for (var i = 0; i < colladaData.TextureCoordinates0.Count; i += colladaData.TextureCoordinateStride)
                {
                    texCoord0Collection.Add(
                        new Vector2(colladaData.TextureCoordinates0[i], colladaData.TextureCoordinates0[i + 1]));
                }

                for (var i = 0; i < colladaData.TextureCoordinates1.Count; i += colladaData.TextureCoordinateStride)
                {
                    texCoord1Collection.Add(new Vector2(colladaData.TextureCoordinates1[i],
                        colladaData.TextureCoordinates1[i + 1]));
                }

                if (!isHousingItem) // housing items do not have bones
                {
                    var errorDict = new Dictionary<int, int>();

                    // Bone index dictionary containing <bone index, index>
                    var boneIndexDict = new Dictionary<int, int>();

                    var boneListIndex = lod0.MeshDataList[meshNum].MeshInfo.BoneListIndex;
                    var boneIndices = xivMdl.BoneIndexMeshList[boneListIndex];

                    // Fill the dictionary with the bone index and its index
                    for (var i = 0; i < boneIndices.BoneIndexCount; i++)
                    {
                        boneIndexDict.Add(boneIndices.BoneIndices[i], i);
                    }

                    var totalBoneCount = 0;

                    try
                    {
                        for (var i = 0; i < positionCollection.Count; i++)
                        {
                            // The number of bones for this vertex
                            var vertBoneCount = colladaData.Vcounts[i];

                            var boneSum = 0;

                            var boneIndexList = new List<byte>();
                            var boneWeightList = new List<byte>();

                            var newBoneCount = 0;
                            for (var j = 0; j < vertBoneCount * 2; j += 2)
                            {
                                // Get bone index from read data
                                var dbi = totalBoneCount * 2 + j;

                                if (!colladaData.BoneIndices.Contains(dbi))
                                {
                                    throw new Exception($"Could not find bone index '{dbi}' in Mesh: {meshNum}");
                                }

                                var dataBoneIndex = colladaData.BoneIndices[dbi];

                                // Get correct bone index from original model data
                                if (!boneIndexDict.ContainsKey(dataBoneIndex))
                                {
                                    throw new Exception($"Mesh: {meshNum} does not contain a bone at index {dataBoneIndex}");
                                }

                                var newBoneIndex = (byte)boneIndexDict[dataBoneIndex];

                                // Get the bone weight for that index
                                var bwi = colladaData.BoneIndices[totalBoneCount * 2 + j + 1];

                                if (!colladaData.BoneWeights.Contains(bwi))
                                {
                                    throw new Exception($"There is no bone weight at index {bwi} in Mesh: {meshNum}");
                                }

                                var boneWeight = (byte)Math.Round(colladaData.BoneWeights[bwi] * 255f);

                                // If the bone weight is not 0 add the index and weight to the list
                                if (boneWeight == 0) continue;

                                boneIndexList.Add(newBoneIndex);
                                boneWeightList.Add(boneWeight);
                                boneSum += boneWeight;
                                newBoneCount++;
                            }

                            // Keep the original bone count, and set vertex bone count to the new bone count
                            var originalBoneCount = vertBoneCount;
                            vertBoneCount = newBoneCount;

                            // Check if the bone count for the vertex is less than 4
                            // Note: The maximum allowed by the mdl file is 4
                            if (vertBoneCount < 4)
                            {
                                var remainder = 4 - vertBoneCount;

                                // Add 0 for any remaining bones to fill up to 4
                                for (var k = 0; k < remainder; k++)
                                {
                                    boneIndexList.Add(0);
                                    boneWeightList.Add(0);
                                }
                            }
                            else if (vertBoneCount > 4)
                            {
                                var extras = vertBoneCount - 4;

                                // Removes any extra bones from the list
                                // This will remove the ones with the smallest weights
                                for (var k = 0; k < extras; k++)
                                {
                                    var min = boneWeightList.Min();
                                    var minIndex = boneWeightList.IndexOf(min);
                                    var count = (boneWeightList.Count(x => x == min));
                                    boneWeightList.Remove(min);
                                    boneIndexList.RemoveAt(minIndex);
                                    boneSum -= min;
                                }
                            }

                            // If the sum of the bones is not 255,
                            // add or remove weight from the largest bone weight
                            if (boneSum != 255)
                            {
                                var diff = boneSum - 255;
                                var max = boneWeightList.Max();
                                var maxIndex = boneWeightList.IndexOf(max);
                                errorDict.Add(i, diff);
                                if (diff < 0)
                                {
                                    boneWeightList[maxIndex] += (byte)Math.Abs(diff);
                                }
                                else
                                {
                                    // Subtract difference when over-weight.
                                    boneWeightList[maxIndex] -= (byte)Math.Abs(diff);
                                }
                            }

                            boneSum = 0;
                            boneWeightList.ForEach(x => boneSum += x);

                            boneIndexCollection.Add(boneIndexList.ToArray());
                            boneWeightCollection.Add(boneWeightList.ToArray());
                            totalBoneCount += originalBoneCount;
                        }
                    }
                    catch
                    {
                        warningsDictionary.Add($"Mismatched Bones {meshNum}",
                            $"It was detected that Mesh: {meshNum}  refrences bones that were not originally part of that mesh.\n\nAn attempt to correct the data was made");

                        totalBoneCount = 0;
                        boneIndexDict.Clear();
                        errorDict.Clear();
                        boneIndexCollection.Clear();
                        boneWeightCollection.Clear();

                        // Use the bone index list for mesh 0 which usually contains all bones
                        boneIndices = xivMdl.BoneIndexMeshList[0];

                        for (var i = 0; i < boneIndices.BoneIndexCount; i++)
                        {
                            boneIndexDict.Add(boneIndices.BoneIndices[i], i);
                        }

                        for (var i = 0; i < positionCollection.Count; i++)
                        {
                            var vertBoneCount = colladaData.Vcounts[i];

                            var boneSum = 0;

                            var boneIndexList = new List<byte>();
                            var boneWeightList = new List<byte>();

                            var newBoneCount = 0;
                            for (var j = 0; j < vertBoneCount * 2; j += 2)
                            {
                                // Get bone index from read data
                                var dbi = totalBoneCount * 2 + j;

                                if (dbi > colladaData.BoneIndices.Count)
                                {
                                    throw new Exception($"Could not find bone index '{dbi}' in Mesh: {meshNum} on second attempt");
                                }

                                var dataBoneIndex = colladaData.BoneIndices[dbi];

                                // Get correct bone index from original model data
                                if (!boneIndexDict.ContainsKey(dataBoneIndex))
                                {
                                    throw new Exception($"Mesh: {meshNum} does not contain a bone at index {dataBoneIndex} on second attempt");
                                }

                                var newBoneIndex = (byte)boneIndexDict[dataBoneIndex];

                                // Get the bone weight for that index
                                var bwi = colladaData.BoneIndices[totalBoneCount * 2 + j + 1];

                                if (bwi > colladaData.BoneWeights.Count)
                                {
                                    throw new Exception($"There is no bone weight at index {bwi} in Mesh: {meshNum}");
                                }

                                var boneWeight = (byte)Math.Round(colladaData.BoneWeights[bwi] * 255f);

                                // If the bone weight is not 0 add the index and weight to the list
                                if (boneWeight == 0) continue;

                                boneIndexList.Add(newBoneIndex);
                                boneWeightList.Add(boneWeight);
                                boneSum += boneWeight;
                                newBoneCount++;
                            }

                            // Keep the original bone count, and set vertex bone count to the new bone count
                            var originalBoneCount = vertBoneCount;
                            vertBoneCount = newBoneCount;

                            // Check if the bone count for the vertex is less than 4
                            // Note: The maximum allowed by the mdl file is 4
                            if (vertBoneCount < 4)
                            {
                                var remainder = 4 - vertBoneCount;

                                for (var k = 0; k < remainder; k++)
                                {
                                    boneIndexList.Add(0);
                                    boneWeightList.Add(0);
                                }
                            }
                            else if (vertBoneCount > 4)
                            {
                                var extras = vertBoneCount - 4;

                                // Removes any extra bones from the list
                                // This will remove the ones with the smallest weights
                                for (var k = 0; k < extras; k++)
                                {
                                    var min = boneWeightList.Min();
                                    var minIndex = boneWeightList.IndexOf(min);
                                    var count = (boneWeightList.Count(x => x == min));
                                    boneWeightList.Remove(min);
                                    boneIndexList.RemoveAt(minIndex);
                                    boneSum -= min;
                                }
                            }

                            // If the sum of the bones is not 255,
                            // add or remove weight from the largest bone weight
                            if (boneSum != 255)
                            {
                                var diff = boneSum - 255;
                                var max = boneWeightList.Max();
                                var maxIndex = boneWeightList.IndexOf(max);
                                errorDict.Add(i, diff);
                                if (diff < 0)
                                {
                                    boneWeightList[maxIndex] += (byte)Math.Abs(diff);
                                }
                                else
                                {
                                    // Subtract difference when over-weight.
                                    boneWeightList[maxIndex] -= (byte)Math.Abs(diff);
                                }
                            }

                            boneSum = 0;
                            boneWeightList.ForEach(x => boneSum += x);

                            boneIndexCollection.Add(boneIndexList.ToArray());
                            boneWeightCollection.Add(boneWeightList.ToArray());
                            totalBoneCount += originalBoneCount;
                        }
                    }

                    // If there were bones that needed to be corrected a string of the corrected data will be made
                    // and added to the warnings dictionary
                    if (errorDict.Count > 0)
                    {
                        var errorString = "";
                        foreach (var er in errorDict)
                        {
                            errorString += "Vertex: " + er.Key + "\t Correction Amount: " + er.Value + "\n";
                        }

                        warningsDictionary.Add($"Weight Correction {meshNum}", "Corrected bone weights on the following vertices :\n\n" + errorString);
                    }
                }

                var meshShapeData = xivMdl.MeshShapeData;

                // Dictionary with <index, index number>
                var indexDict = new Dictionary<int, int>();
                var indexNum = 0;

                // Each item in this list contains the index for each data point
                var indexList = new List<int[]>();

                var stride = 5;
                var indexMax = 0;

                if (texCoord1Collection.Count < 1)
                {
                    stride = 4;
                }

                for (var i = 0; i < colladaData.Indices.Count; i += stride)
                {
                    indexList.Add(colladaData.Indices.GetRange(i, stride).ToArray());
                }

                if (colladaData.Indices.Count > 0)
                {
                    indexMax = colladaData.Indices.Max();
                }

                // Create the new data point lists in their appropriate order from their indices
                for (var i = 0; i < indexList.Count; i++)
                {
                    // Skip this index if it does not exist in the index list
                    if (indexDict.ContainsKey(indexList[i][0])) continue;

                    if (!colladaData.IsBlender)
                    {
                        var pos0 = indexList[i][0];
                        var pos1 = indexList[i][1];
                        var pos2 = indexList[i][2];

                        // Dictionary with <index, index number>
                        indexDict.Add(pos0, indexNum);

                        // If the index at index 0 is larger than the position collection, throw an exception
                        if (pos0 > positionCollection.Count)
                        {
                            throw new IndexOutOfRangeException($"There is no position at index {pos0},  position count: {positionCollection.Count}");
                        }
                        nPositionCollection.Add(positionCollection[pos0]);

                        if (!isHousingItem) // Housing items do not have bones
                        {
                            // If the index at index 0 is larger than the bone index collection, throw an exception
                            if (pos0 > boneIndexCollection.Count)
                            {
                                throw new IndexOutOfRangeException($"There is no bone index at index {pos0},  bone index count: {boneIndexCollection.Count}");
                            }
                            nBoneIndexCollection.Add(boneIndexCollection[pos0]);

                            // If the index at index 0 is larger than the bone weight collection, throw an exception
                            if (pos0 > boneWeightCollection.Count)
                            {
                                throw new IndexOutOfRangeException($"There is no bone weight at index {pos0},  bone weight count: {boneWeightCollection.Count}");
                            }
                            nBoneWeightCollection.Add(boneWeightCollection[pos0]);
                        }

                        // If the index at index 1 is larger than the normals collection, throw an exception
                        if (pos1 > normalsCollection.Count)
                        {
                            throw new IndexOutOfRangeException($"There is no normal at index {pos1},  normal count: {normalsCollection.Count}");
                        }
                        nNormalsCollection.Add(normalsCollection[pos1]);

                        // If the index at index 2 is larger than the texture coordinate 0 collection, throw an exception
                        if (pos2 > texCoord0Collection.Count)
                        {
                            throw new IndexOutOfRangeException($"There is no texture coordinate 0 at index {pos2},  texture coordinate 0 count: {texCoord0Collection.Count}");
                        }
                        nTexCoord0Collection.Add(texCoord0Collection[pos2]);

                        if (texCoord1Collection.Count > 0)
                        {
                            var pos3 = indexList[i][3];

                            // If the index at index 3 is larger than the texture coordinate 1 collection, throw an exception
                            if (pos3 > texCoord1Collection.Count)
                            {
                                throw new IndexOutOfRangeException($"There is no texture coordinate 1 at index {pos3},  texture coordinate 1 count: {texCoord1Collection.Count}");
                            }
                            nTexCoord1Collection.Add(texCoord1Collection[pos3]);
                        }

                        // If there are secondary texture coordinates, we adjust the index position as necessary
                        var nPos = texCoord1Collection.Count > 0 ? indexList[i][4] : indexList[i][3];

                        if (tangentsCollection.Count > 0)
                        {
                            // If the index at index n is larger than the tangents collection, throw an exception
                            if (nPos > tangentsCollection.Count)
                            {
                                throw new IndexOutOfRangeException($"There is no tangent at index {nPos},  tangent count: {tangentsCollection.Count}");
                            }
                            nTangentsCollection.Add(tangentsCollection[nPos]);
                        }

                        if (biNormalsCollection.Count > 0)
                        {
                            // If the index at index n is larger than the binormals collection, throw an exception
                            if (nPos > biNormalsCollection.Count)
                            {
                                throw new IndexOutOfRangeException($"There is no binormal at index {nPos},  binormal count: {biNormalsCollection.Count}");
                            }
                            nBiNormalsCollection.Add(biNormalsCollection[nPos]);
                        }
                    }
                    // For blender there is only 1 index for all data points
                    else
                    {
                        var pos0 = indexList[i][0];

                        // Dictionary with <index, index number>
                        indexDict.Add(pos0, indexNum);

                        // If the index at index 0 is larger than the position collection, throw an exception
                        if (pos0 > positionCollection.Count)
                        {
                            throw new IndexOutOfRangeException($"There is no position at index {pos0},  position count: {positionCollection.Count}");
                        }
                        nPositionCollection.Add(positionCollection[pos0]);

                        if (!isHousingItem) // Housing items do not have bones
                        {
                            // If the index at index 0 is larger than the bone index collection, throw an exception
                            if (pos0 > boneIndexCollection.Count)
                            {
                                throw new IndexOutOfRangeException($"There is no bone index at index {pos0},  bone index count: {boneIndexCollection.Count}");
                            }
                            nBoneIndexCollection.Add(boneIndexCollection[pos0]);

                            // If the index at index 0 is larger than the bone weight collection, throw an exception
                            if (pos0 > boneWeightCollection.Count)
                            {
                                throw new IndexOutOfRangeException($"There is no bone weight at index {pos0},  bone weight count: {boneWeightCollection.Count}");
                            }
                            nBoneWeightCollection.Add(boneWeightCollection[pos0]);
                        }

                        // If the index at index 0 is larger than the normals collection, throw an exception
                        if (pos0 > normalsCollection.Count)
                        {
                            throw new IndexOutOfRangeException($"There is no normals at index {pos0},  normals count: {normalsCollection.Count}");
                        }
                        nNormalsCollection.Add(normalsCollection[pos0]);

                        // If the index at index 0 is larger than the texture coordinates 0 collection, throw an exception
                        if (pos0 > texCoord0Collection.Count)
                        {
                            throw new IndexOutOfRangeException($"There is no texture coordinates 0 at index {pos0},  texture coordinates 0 count: {texCoord0Collection.Count}");
                        }
                        nTexCoord0Collection.Add(texCoord0Collection[pos0]);

                        if (texCoord1Collection.Count > 0)
                        {
                            // If the index at index 0 is larger than the texture coordinates 1 collection, throw an exception
                            if (pos0 > texCoord1Collection.Count)
                            {
                                throw new IndexOutOfRangeException($"There is no texture coordinates 1 at index {pos0},  texture coordinates 1 count: {texCoord1Collection.Count}");
                            }
                            nTexCoord1Collection.Add(texCoord1Collection[pos0]);
                        }

                        if (tangentsCollection.Count > 0)
                        {
                            // If the index at index 0 is larger than the tangents collection, throw an exception
                            if (pos0 > tangentsCollection.Count)
                            {
                                throw new IndexOutOfRangeException($"There is no tangents at index {pos0},  tangents count: {tangentsCollection.Count}");
                            }
                            nTangentsCollection.Add(tangentsCollection[pos0]);

                            // If the index at index 0 is larger than the binormals collection, throw an exception
                            if (pos0 > biNormalsCollection.Count)
                            {
                                throw new IndexOutOfRangeException($"There is no binormals at index {pos0},  binormals count: {biNormalsCollection.Count}");
                            }
                            nBiNormalsCollection.Add(biNormalsCollection[pos0]);
                        }
                    }

                    indexNum++;
                }

                var nPositionsList = new HashSet<int>();

                // Remake the indices
                indexCollection.Clear();
                for (var i = 0; i < indexList.Count; i++)
                {
                    if (!indexDict.ContainsKey(indexList[i][0]))
                    {
                        throw new Exception($"Could not find index {i} in the index dictionary");
                    }

                    var nIndex = indexDict[indexList[i][0]];
                    indexCollection.Add(nIndex);
                }

                var referencePositionDictionary = lod0.MeshDataList[meshNum].ReferencePositionsDictionary;

                // If there are advanced import settings available and the current mesh is in the settings
                if (advImportSettings != null && advImportSettings.ContainsKey(meshNum.ToString()))
                {
                    // If the fix option is selected for either all meshes or this mesh
                    if (advImportSettings[XivStrings.All].Fix || advImportSettings[meshNum.ToString()].Fix)
                    {
                        foreach (var referencePosition in referencePositionDictionary)
                        {
                            var a = 0;
                            foreach (var v in nPositionCollection)
                            {
                                var found = false;
                                if (Vector3.NearEqual(referencePosition.Value, v, new Vector3(0.02f)))
                                {
                                    for (var i = 0; i < indexCollection.Count; i++)
                                    {
                                        if (a == indexCollection[i] && !nPositionsList.Contains(i) && !meshShapeDictionary.ContainsKey(referencePosition.Key))
                                        {
                                            meshShapeDictionary.Add(referencePosition.Key, i);
                                            nPositionsList.Add(i);
                                            found = true;
                                            break;

                                        }

                                    }

                                    if (found)
                                    {
                                        break;
                                    }
                                }
                                a++;
                            }
                        }
                    }
                }

                var meshGeometry = new MeshGeometry3D
                {
                    Positions = nPositionCollection,
                    Indices = indexCollection,
                    Normals = nNormalsCollection,
                    TextureCoordinates = nTexCoord0Collection
                };

                // Try to compute the tangents and bitangents for the mesh
                try
                {
                    MeshBuilder.ComputeTangents(meshGeometry);
                }
                catch (Exception e)
                {
                    throw new Exception($"There was an error computing the tangents for the model. {e.Message}");
                }

                /* Computing the Tangents using the above method has given better results
                 * than the data directly from the imported dae file.
                 *
                 * We can use the data directly from the dae file by uncommenting the lines below
                 * and commenting out the lines above.
                 */

                //if (cd.biNormal.Count > 0)
                //{
                //    mg.BiTangents = nBiNormals;
                //}

                //if (cd.tangent.Count > 0)
                //{
                //    mg.Tangents = Tangents;
                //}


                // Computing the BiTangents with the below calculations have given better results
                // than using the data directly from the dae file above
                var tan1 = new Vector3[positionCollection.Count];
                var tan2 = new Vector3[positionCollection.Count];
                for (var a = 0; a < indexCollection.Count; a += 3)
                {
                    var i1 = indexCollection[a];
                    var i2 = indexCollection[a + 1];
                    var i3 = indexCollection[a + 2];
                    var v1 = nPositionCollection[i1];
                    var v2 = nPositionCollection[i2];
                    var v3 = nPositionCollection[i3];
                    var w1 = nTexCoord0Collection[i1];
                    var w2 = nTexCoord0Collection[i2];
                    var w3 = nTexCoord0Collection[i3];
                    var x1 = v2.X - v1.X;
                    var x2 = v3.X - v1.X;
                    var y1 = v2.Y - v1.Y;
                    var y2 = v3.Y - v1.Y;
                    var z1 = v2.Z - v1.Z;
                    var z2 = v3.Z - v1.Z;
                    var s1 = w2.X - w1.X;
                    var s2 = w3.X - w1.X;
                    var t1 = w2.Y - w1.Y;
                    var t2 = w3.Y - w1.Y;
                    var r = 1.0f / (s1 * t2 - s2 * t1);
                    var sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                    var tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);
                    tan1[i1] += sdir;
                    tan1[i2] += sdir;
                    tan1[i3] += sdir;
                    tan2[i1] += tdir;
                    tan2[i2] += tdir;
                    tan2[i3] += tdir;
                }

                var colladaMeshData = new ColladaMeshData();

                for (var a = 0; a < nPositionCollection.Count; ++a)
                {
                    var n = Vector3.Normalize(nNormalsCollection[a]);
                    var t = Vector3.Normalize(tan1[a]);
                    var d = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
                    var tmpt = new Vector3(t.X, t.Y, t.Z);
                    meshGeometry.BiTangents.Add(tmpt);
                    colladaMeshData.Handedness.Add((int)d);
                }
                colladaMeshData.MeshGeometry = meshGeometry;
                colladaMeshData.BoneIndices = nBoneIndexCollection;
                colladaMeshData.BoneWeights = nBoneWeightCollection;
                colladaMeshData.PartsDictionary = colladaData.PartsDictionary;
                colladaMeshData.TextureCoordintes1 = nTexCoord1Collection;

                colladaMeshDataList.Add(colladaMeshData);

                meshNum++;
            }

            MakeNewMdlFile(colladaMeshDataList, item, xivMdl, advImportSettings, source, rawDataOnly);

            return warningsDictionary;
        }

        /// <summary>
        /// Creates a new Mdl file from the given data
        /// </summary>
        /// <param name="colladaMeshDataList">The list of mesh data obtained from the imported collada file</param>
        /// <param name="item">The item the model belongs to</param>
        /// <param name="xivMdl">The original model data</param>
        /// <param name="importSettings">The import settings if any</param>
        /// <param name="source">The source/application that is writing to the dat.</param>
        private void MakeNewMdlFile(List<ColladaMeshData> colladaMeshDataList, IItemModel item, XivMdl xivMdl, 
            Dictionary<string, ModelImportSettings> importSettings, string source, bool rawDataOnly)
        {
            var modding = new Modding(_gameDirectory);

            var isAlreadyModified = false;

            var itemType = ItemType.GetItemType(item);

            var mdlPath = Path.Combine(xivMdl.MdlPath.Folder, xivMdl.MdlPath.File);

            var modEntry = modding.TryGetModEntry(mdlPath);

            // Vertex Info
            #region Vertex Info Block

            var vertexInfoBlock = new List<byte>();
            var vertexInfoDict = new Dictionary<int, Dictionary<VertexUsageType, VertexDataType>>();

            var lodNum = 0;
            foreach (var lod in xivMdl.LoDList)
            {
                var vdsDictionary = new Dictionary<VertexUsageType, VertexDataType>();
                foreach (var meshData in lod.MeshDataList)
                {
                    var dataSize = 0;
                    foreach (var vds in meshData.VertexDataStructList)
                    {
                        // Padding
                        vertexInfoBlock.AddRange(new byte[4]);

                        var dataBlock  = vds.DataBlock;
                        var dataOffset = vds.DataOffset;
                        var dataType   = vds.DataType;
                        var dataUsage  = vds.DataUsage;

                        if (lodNum == 0)
                        {
                            // Change Normals to Float from its default of Half for greater accuracy
                            // This increases the data from 8 bytes to 12 bytes
                            if (dataUsage == VertexUsageType.Normal)
                            {
                                // If the data type is already Float3 (in the case of an already modified model)
                                // we skip it.
                                if (dataType != VertexDataType.Float3)
                                {
                                    dataType = VertexDataType.Float3;
                                }
                                else
                                {
                                    isAlreadyModified = true;
                                }
                            }

                            // Change Texture Coordinates to Float from its default of Half for greater accuracy
                            // This increases the data from 8 bytes to 16 bytes, or from 4 bytes to 8 bytes if it is a housing item
                            if (dataUsage == VertexUsageType.TextureCoordinate)
                            {
                                if (dataType == VertexDataType.Half2 || dataType == VertexDataType.Half4)
                                {
                                    if (dataType == VertexDataType.Half2)
                                    {
                                        dataType = VertexDataType.Float2;
                                    }
                                    else
                                    {
                                        dataType = VertexDataType.Float4;
                                    }
                                }
                                else
                                {
                                    isAlreadyModified = true;
                                }
                            }

                            // We have to adjust each offset after the Normal value because its size changed
                            // Normal is always in data block 1 and the first so its offset is 0
                            // Note: Texture Coordinates are always last so there is no need to adjust for it
                            if (dataBlock == 1 && dataOffset > 0 && !isAlreadyModified)
                            {
                                dataOffset += 4;
                            }
                        }

                        vertexInfoBlock.Add(vds.DataBlock);
                        vertexInfoBlock.Add((byte)dataOffset);
                        vertexInfoBlock.Add((byte)dataType);
                        vertexInfoBlock.Add((byte)dataUsage);

                        if (!vdsDictionary.ContainsKey(dataUsage))
                        {
                            vdsDictionary.Add(dataUsage, dataType);
                        }

                        dataSize += 8;
                    }

                    // End flag
                    vertexInfoBlock.AddRange(new byte[4]);
                    vertexInfoBlock.Add(0xFF);
                    vertexInfoBlock.AddRange(new byte[3]);

                    dataSize += 8;

                    if (dataSize < 64)
                    {
                        var remaining = 64 - dataSize;

                        vertexInfoBlock.AddRange(new byte[remaining]);
                    }

                    // Padding between data
                    vertexInfoBlock.AddRange(new byte[72]);
                }

                vertexInfoDict.Add(lodNum, vdsDictionary);

                lodNum++;
            }

            // The first vertex info block does not have padding so we remove it and add it at the end
            vertexInfoBlock.RemoveRange(0, 4);
            vertexInfoBlock.AddRange(new byte[4]);
            #endregion

            // All of the data blocks for the model data
            var fullModelDataBlock = new List<byte>();

            // Path Data
            #region Path Info Block

            var pathInfoBlock = new List<byte>();

            // Path Count
            pathInfoBlock.AddRange(BitConverter.GetBytes(xivMdl.PathData.PathCount));

            // Path Block Size
            pathInfoBlock.AddRange(BitConverter.GetBytes(xivMdl.PathData.PathBlockSize));

            // Attribute paths
            foreach (var atr in xivMdl.PathData.AttributeList)
            {
                // Path converted to bytes
                pathInfoBlock.AddRange(Encoding.UTF8.GetBytes(atr));

                // Byte between paths
                pathInfoBlock.Add(0);
            }

            // Bone paths
            foreach (var bone in xivMdl.PathData.BoneList)
            {
                // Path converted to bytes
                pathInfoBlock.AddRange(Encoding.UTF8.GetBytes(bone));

                // Byte between paths
                pathInfoBlock.Add(0);
            }

            // Material paths
            foreach (var material in xivMdl.PathData.MaterialList)
            {
                // Path converted to bytes
                pathInfoBlock.AddRange(Encoding.UTF8.GetBytes(material));

                // Byte between paths
                pathInfoBlock.Add(0);
            }

            // Extra paths
            foreach (var extra in xivMdl.PathData.ExtraPathList)
            {
                // Path converted to bytes
                pathInfoBlock.AddRange(Encoding.UTF8.GetBytes(extra));

                // Byte between paths
                pathInfoBlock.Add(0);
            }

            // Padding before next section
            var pathPadding = xivMdl.PathData.PathBlockSize - (pathInfoBlock.Count - 8);
            pathInfoBlock.AddRange(new byte[pathPadding]);

            fullModelDataBlock.AddRange(pathInfoBlock);
            #endregion

            // Model Data
            #region Model Data Block

            var modelDataBlock = new List<byte>();

            var modelData = xivMdl.ModelData;

            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown0));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.MeshCount));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.AttributeCount));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.MeshPartCount));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.MaterialCount));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.BoneCount));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.BoneListCount));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.ShapeCount));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.ShapeDataCount));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.ShapeIndexCount));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown1));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown2));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown3));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown4));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown5));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown6));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown7));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown8));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown9));
            modelDataBlock.Add(modelData.Unknown10a);
            modelDataBlock.Add(modelData.Unknown10b);
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown11));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown12));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown13));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown14));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown15));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown16));
            modelDataBlock.AddRange(BitConverter.GetBytes(modelData.Unknown17));

            fullModelDataBlock.AddRange(modelDataBlock);

            #endregion

            // Unknown Data 0
            #region Unknown Data Block 0

            var unknownDataBlock0 = xivMdl.UnkData0.Unknown;

            fullModelDataBlock.AddRange(unknownDataBlock0);

            #endregion

            // Get the imported data
            var importDataDictionary = GetImportData(colladaMeshDataList, itemType, vertexInfoDict);

            // Level of Detail
            #region Level of Detail Block

            var lodDataBlock = new List<byte>();

            lodNum = 0;
            var importVertexDataSize     = 0;
            var importIndexDataSize      = 0;
            var previousVertexDataSize   = 0;
            var previousindexDataSize    = 0;
            var previousVertexDataOffset = 0;

            foreach (var lod in xivMdl.LoDList)
            {
                // Index Data Size is recalculated for LoD 0, because of the imported data, but remains the same
                // for every other LoD.
                var indexDataSize = lod.IndexDataSize;

                // Both of these index values are always the same.
                // Because index data starts after the vertex data, these values need to be recalculated because
                // the imported data can add/remove vertex data
                var indexDataStart = lod.IndexDataStart;
                var indexDataOffset = lod.IndexDataOffset;


                // Vertex Data Offset would need to be changed for LoD 0 if any data is added to any MDL data block.
                // As of now, we only modify existing data blocks but do not change their size, so no changes are made
                // to the value for LoD 0.
                // This value is recalcualted for every other LoD because of the imported data can add/remove vertex data.
                var vertexDataOffset = lod.VertexDataOffset;

                // Vertex Data Size is recalculated for LoD 0, because of the imported data, but remains the same
                // for every other LoD.
                var vertexDataSize = lod.VertexDataSize;

                // Calculate the new values based on imported data
                // Note: Only the highest quality LoD is used which is LoD 0
                if (lodNum == 0)
                {
                    // Get the sum of the vertex data and indices for all meshes in the imported data
                    foreach (var importData in importDataDictionary)
                    {
                        var meshData = lod.MeshDataList[importData.Key];
                        var shapeDataCount = 0;
                        if (meshData.ShapePositionsDictionary != null)
                        {
                            var entrySizeSum = meshData.MeshInfo.VertexDataEntrySize0 + meshData.MeshInfo.VertexDataEntrySize1;
                            if (!isAlreadyModified)
                            {
                                var texCoordDataType = vertexInfoDict[0][VertexUsageType.TextureCoordinate];

                                if (texCoordDataType == VertexDataType.Float2)
                                {
                                    entrySizeSum += 8;
                                }
                                else
                                {
                                    entrySizeSum += 12;
                                }
                            }

                            shapeDataCount = meshData.ShapePositionsDictionary.Count * entrySizeSum;
                        }

                        importVertexDataSize += importData.Value.VertexData0.Count + importData.Value.VertexData1.Count + shapeDataCount;

                        var indexPadding = 16 - importData.Value.IndexData.Count % 16;
                        if (indexPadding == 16)
                        {
                            indexPadding = 0;
                        }

                        importIndexDataSize += importData.Value.IndexData.Count + indexPadding;
                    }

                    vertexDataSize = importVertexDataSize;
                    indexDataSize = importIndexDataSize;

                    indexDataOffset = vertexDataOffset + vertexDataSize;
                    indexDataStart = indexDataOffset;
                }
                else
                {
                    // The (vertex offset + vertex data size + index data size) of the previous LoD give you the vertex offset of the current LoD
                    vertexDataOffset = previousVertexDataOffset + previousVertexDataSize + previousindexDataSize;

                    // The (vertex data offset + vertex data size) of the current LoD give you the index offset
                    // In this case it uses the newly calulated vertex data offset to get the correct index offset
                    indexDataOffset = vertexDataOffset + vertexDataSize;
                    indexDataStart = indexDataOffset;
                }

                // These two values would need to be changed for every LoD if additional meshes were to be added
                lodDataBlock.AddRange(BitConverter.GetBytes(lod.MeshOffset));
                lodDataBlock.AddRange(BitConverter.GetBytes(lod.MeshCount));

                lodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown0));
                lodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown1));
                lodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown2));
                lodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown3));
                lodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown4));
                lodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown5));
                lodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown6));

                lodDataBlock.AddRange(BitConverter.GetBytes(indexDataStart));

                lodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown7));
                lodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown8));

                lodDataBlock.AddRange(BitConverter.GetBytes(vertexDataSize));
                lodDataBlock.AddRange(BitConverter.GetBytes(indexDataSize));
                lodDataBlock.AddRange(BitConverter.GetBytes(vertexDataOffset));
                lodDataBlock.AddRange(BitConverter.GetBytes(indexDataOffset));

                previousVertexDataSize   = vertexDataSize;
                previousindexDataSize    = indexDataSize;
                previousVertexDataOffset = vertexDataOffset;

                lodNum++;
            }

            fullModelDataBlock.AddRange(lodDataBlock);

            #endregion

            // Extra LoD Data
            #region Extra Level Of Detail Block

            if (xivMdl.ExtraLoDList != null && xivMdl.ExtraLoDList.Count > 0)
            {
                var extraLodDataBlock = new List<byte>();

                foreach (var lod in xivMdl.ExtraLoDList)
                {
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.MeshOffset));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.MeshCount));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown0));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown1));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown2));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown3));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown4));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown5));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown6));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.IndexDataStart));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown7));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.Unknown8));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.VertexDataSize));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.IndexDataSize));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.VertexDataOffset));
                    extraLodDataBlock.AddRange(BitConverter.GetBytes(lod.IndexDataOffset));
                }

                fullModelDataBlock.AddRange(extraLodDataBlock);
            }
            #endregion


            // Mesh Data
            #region Mesh Data Block

            var meshDataBlock = new List<byte>();

            lodNum = 0;
            foreach (var lod in xivMdl.LoDList)
            {
                var meshNum = 0;
                var previousVertexDataOffset1 = 0;
                var previousIndexDataOffset = 0;
                var previousIndexCount = 0;
                foreach (var meshData in lod.MeshDataList)
                {
                    var meshInfo = meshData.MeshInfo;

                    var vertexCount = meshInfo.VertexCount;
                    var indexCount = meshInfo.IndexCount;
                    var indexDataOffset = meshInfo.IndexDataOffset;
                    var vertexDataOffset0 = meshInfo.VertexDataOffset0;
                    var vertexDataOffset1 = meshInfo.VertexDataOffset1;
                    var vertexDataOffset2 = meshInfo.VertexDataOffset2;
                    var vertexDataEntrySize0 = meshInfo.VertexDataEntrySize0;
                    var vertexDataEntrySize1 = meshInfo.VertexDataEntrySize1;
                    var vertexDataEntrySize2 = meshInfo.VertexDataEntrySize2;


                    if (lodNum == 0)
                    {
                        vertexCount = importDataDictionary[meshNum].VertexCount;
                        indexCount = importDataDictionary[meshNum].IndexCount;

                        // Since we changed Normals and Texture coordinates from half to floats, we need
                        // to adjust the vertex data entry size for that data block from 24 to 36, or from 16 to 24 if its a housing item
                        // we skip this adjustment if the model is already modified
                        if (!isAlreadyModified)
                        {
                            var texCoordDataType = vertexInfoDict[0][VertexUsageType.TextureCoordinate];

                            if (texCoordDataType == VertexDataType.Float2)
                            {
                                vertexDataEntrySize1 += 8;
                            }
                            else
                            {
                                vertexDataEntrySize1 += 12;
                            }
                        }

                        if (xivMdl.HasShapeData && meshData.ShapePositionsDictionary != null)
                        {
                            // The shape positions count is added to the vertex count because it is not exported and therefore
                            // missing from the imported data.
                            vertexCount += meshData.ShapePositionsDictionary.Count;

                            importDataDictionary[meshNum].VertexCount = vertexCount;
                        }

                        // Calculate new index data offset
                        if (meshNum > 0)
                        {
                            // Padding used after index data block
                            var indexPadding = 8 - previousIndexCount % 8;

                            if (indexPadding == 8)
                            {
                                indexPadding = 0;
                            }

                            indexDataOffset = previousIndexDataOffset + previousIndexCount + indexPadding;
                        }

                        // Calculate new Vertex Data Offsets
                        if (meshNum > 0)
                        {
                            vertexDataOffset0 = previousVertexDataOffset1 +
                                                importDataDictionary[meshNum - 1].VertexCount * vertexDataEntrySize1;

                            vertexDataOffset1 = vertexDataOffset0 + vertexCount * vertexDataEntrySize0;

                        }
                        else
                        {
                            vertexDataOffset1 = vertexCount * vertexDataEntrySize0;
                        }
                    }

                    meshDataBlock.AddRange(BitConverter.GetBytes(vertexCount));
                    meshDataBlock.AddRange(BitConverter.GetBytes(indexCount));
                    meshDataBlock.AddRange(BitConverter.GetBytes(meshInfo.MaterialIndex));

                    // These 2 Mesh Part entries would have to change if additional mesh parts were to be added in the future
                    meshDataBlock.AddRange(BitConverter.GetBytes(meshInfo.MeshPartIndex));
                    meshDataBlock.AddRange(BitConverter.GetBytes(meshInfo.MeshPartCount));

                    meshDataBlock.AddRange(BitConverter.GetBytes(meshInfo.BoneListIndex));
                    meshDataBlock.AddRange(BitConverter.GetBytes(indexDataOffset));
                    meshDataBlock.AddRange(BitConverter.GetBytes(vertexDataOffset0));
                    meshDataBlock.AddRange(BitConverter.GetBytes(vertexDataOffset1));
                    meshDataBlock.AddRange(BitConverter.GetBytes(vertexDataOffset2));
                    meshDataBlock.Add(vertexDataEntrySize0);
                    meshDataBlock.Add(vertexDataEntrySize1);
                    meshDataBlock.Add(vertexDataEntrySize2);
                    meshDataBlock.Add(meshInfo.VertexDataBlockCount);

                    previousVertexDataOffset1 = vertexDataOffset1;
                    previousIndexDataOffset = indexDataOffset;
                    previousIndexCount = indexCount;

                    meshNum++;
                }

                lodNum++;
            }

            fullModelDataBlock.AddRange(meshDataBlock);

            #endregion

            // Unknown Attribute Data
            #region Attribute Data Block

            foreach (var attributeOffset in xivMdl.AttrDataBlock.AttributePathOffsetList)
            {
                fullModelDataBlock.AddRange(BitConverter.GetBytes(attributeOffset));
            }

            #endregion

            // Unknown Data 1
            #region Unknown Data Block 1

            var unknownDataBlock1 = xivMdl.UnkData1.Unknown;

            if (unknownDataBlock1 != null)
            {
                fullModelDataBlock.AddRange(unknownDataBlock1);
            }
            #endregion

            // Mesh Part
            #region Mesh Part Data Block

            var meshPartDataBlock = new List<byte>();

            lodNum = 0;
            var partPadding = 0;
            foreach (var lod in xivMdl.LoDList)
            {
                var meshNum = 0;
                var previousIndexOffset = 0;
                var previousIndexCount = 0;
                foreach (var meshData in lod.MeshDataList)
                {
                    var partCount = meshData.MeshPartList.Count;
                    var partNum = 0;
                    foreach (var meshPart in meshData.MeshPartList)
                    {
                        var indexOffset = meshPart.IndexOffset;
                        var indexCount = meshPart.IndexCount;
                        var attributeIndex = meshPart.AttributeIndex;

                        if (lodNum == 0)
                        {
                            var importedPartsDictionary = colladaMeshDataList[meshNum].PartsDictionary;

                           // Recalculate Index Offset
                            if (meshNum == 0)
                            {
                                if (partNum > 0)
                                {
                                    indexOffset = previousIndexOffset + previousIndexCount;
                                }
                            }
                            else
                            {
                                indexOffset = previousIndexOffset + previousIndexCount + partPadding;
                            }

                            // Recalculate Index Count
                            indexCount = importedPartsDictionary.ContainsKey(partNum) ? importedPartsDictionary[partNum] : 0;

                            // Calculate padding between meshes
                            if (partNum == partCount - 1)
                            {
                                var padd = (indexOffset + indexCount) % 8;

                                if (padd != 0)
                                {
                                    partPadding = 8 - padd;
                                }
                                else
                                {
                                    partPadding = 0;
                                }
                            }

                            // Change attribute index if advanced import settings are present
                            if (importSettings != null && importSettings.ContainsKey(meshNum.ToString()))
                            {
                                if (importSettings[meshNum.ToString()].PartDictionary != null)
                                {
                                    attributeIndex = importSettings[meshNum.ToString()].PartDictionary[partNum];
                                }
                            }
                        }

                        meshPartDataBlock.AddRange(BitConverter.GetBytes(indexOffset));
                        meshPartDataBlock.AddRange(BitConverter.GetBytes(indexCount));
                        meshPartDataBlock.AddRange(BitConverter.GetBytes(attributeIndex));
                        meshPartDataBlock.AddRange(BitConverter.GetBytes(meshPart.BoneStartOffset));
                        meshPartDataBlock.AddRange(BitConverter.GetBytes(meshPart.BoneCount));

                        previousIndexCount = indexCount;
                        previousIndexOffset = indexOffset;

                        partNum++;
                    }
                    meshNum++;
                }
                lodNum++;
            }

            fullModelDataBlock.AddRange(meshPartDataBlock);

            #endregion

            // Unknown Data 2
            #region Unknown Data Block 2

            var unknownDataBlock2 = xivMdl.UnkData2.Unknown;

            fullModelDataBlock.AddRange(unknownDataBlock2);

            #endregion

            // Unknown Material Data
            #region Material Data Block

            foreach (var materialOffset in xivMdl.MatDataBlock.MaterialPathOffsetList)
            {
                fullModelDataBlock.AddRange(BitConverter.GetBytes(materialOffset));
            }

            #endregion

            // Unknown Bone Data
            #region Bone Data Block

            foreach (var boneOffset in xivMdl.BonDataBlock.BonePathOffsetList)
            {
                fullModelDataBlock.AddRange(BitConverter.GetBytes(boneOffset));
            }

            #endregion

            // Bone Indices for meshes
            #region Bone Index Mesh Block

            var boneIndexMeshBlock = new List<byte>();

            foreach (var boneIndexMesh in xivMdl.BoneIndexMeshList)
            {
                foreach (var boneIndex in boneIndexMesh.BoneIndices)
                {
                    boneIndexMeshBlock.AddRange(BitConverter.GetBytes(boneIndex));
                }

                boneIndexMeshBlock.AddRange(BitConverter.GetBytes(boneIndexMesh.BoneIndexCount));
            }

            fullModelDataBlock.AddRange(boneIndexMeshBlock);

            #endregion

            #region Shape Data Block

            if (xivMdl.HasShapeData)
            {
                // Mesh Shape Info
                #region Mesh Shape Info Data Block

                var meshShapeInfoDataBlock = new List<byte>();

                var shapeInfoCount = xivMdl.MeshShapeData.ShapeInfoList.Count;

                foreach (var info in xivMdl.MeshShapeData.ShapeInfoList)
                {
                    meshShapeInfoDataBlock.AddRange(BitConverter.GetBytes(info.ShapePathOffset));

                    foreach (var shapeInfoShapeIndexPart in info.ShapeIndexParts)
                    {
                        meshShapeInfoDataBlock.AddRange(BitConverter.GetBytes(shapeInfoShapeIndexPart.DataInfoIndex));
                    }

                    foreach (var shapeInfoShapeIndexPart in info.ShapeIndexParts)
                    {
                        meshShapeInfoDataBlock.AddRange(BitConverter.GetBytes(shapeInfoShapeIndexPart.PartCount));
                    }
                }

                fullModelDataBlock.AddRange(meshShapeInfoDataBlock);

                #endregion

                // Mesh Shape Index Info
                #region Mesh Index Info Data Block

                var meshShapeIndexInfoDataBlock = new List<byte>();

                foreach (var shapeIndexInfo in xivMdl.MeshShapeData.ShapeDataInfoList)
                {
                    meshShapeIndexInfoDataBlock.AddRange(BitConverter.GetBytes(shapeIndexInfo.IndexDataOffset));
                    meshShapeIndexInfoDataBlock.AddRange(BitConverter.GetBytes(shapeIndexInfo.IndexCount));
                    meshShapeIndexInfoDataBlock.AddRange(BitConverter.GetBytes(shapeIndexInfo.DataIndexOffset));
                }

                fullModelDataBlock.AddRange(meshShapeIndexInfoDataBlock);

                #endregion

                // Mesh Shape Data
                #region Mesh Shape Data Block

                var meshShapeDataBlock = new List<byte>();

                var lodNumber = 0;
                foreach (var lod in xivMdl.LoDList)
                {
                    var indexMeshNum = new Dictionary<int, int>();

                    // Get the index data offsets in each mesh
                    for (var i = 0; i < lod.MeshCount; i++)
                    {
                        var indexDataOffset = lod.MeshDataList[i].MeshInfo.IndexDataOffset;

                        indexMeshNum.Add(indexDataOffset, i);
                    }

                    // Number of shape info in each mesh
                    var perMeshCount = xivMdl.ModelData.ShapeCount;

                    // Shape info list
                    var shapeInfoList = xivMdl.MeshShapeData.ShapeInfoList;

                    for (var j = 0; j < perMeshCount; j++)
                    {
                        var shapeInfo = shapeInfoList[j];

                        var indexPart = shapeInfo.ShapeIndexParts[lodNumber];

                        // The part count
                        var infoPartCount = indexPart.PartCount;

                        for (var k = 0; k < infoPartCount; k++)
                        {
                            // Gets the data info for the part
                            var shapeDataInfo = xivMdl.MeshShapeData.ShapeDataInfoList[indexPart.DataInfoIndex + k];

                            // The offset in the shape data 
                            var indexDataOffset = shapeDataInfo.IndexDataOffset;

                            var indexMeshLocation = indexMeshNum[indexDataOffset];

                            // Get the mesh data
                            var mesh = lod.MeshDataList[indexMeshLocation];

                            var shapeData = mesh.ShapeIndexOffsetDictionary[shapeDataInfo.DataIndexOffset];

                            if (importSettings != null && importSettings.ContainsKey(mesh.ToString()))
                            {
                                var shapeCount = mesh.ShapeIndexOffsetDictionary.Count;

                                if (importSettings[XivStrings.All].Disable || importSettings[mesh.ToString()].Disable)
                                {
                                    foreach (var indexOffset in shapeData)
                                    {
                                        meshShapeDataBlock.AddRange(BitConverter.GetBytes((short)0));
                                        meshShapeDataBlock.AddRange(BitConverter.GetBytes((short)0));
                                    }
                                }
                                else if (importSettings[XivStrings.All].Fix || importSettings[mesh.ToString()].Fix)
                                {
                                    throw new NotImplementedException();

                                    //TODO Implement Fix
                                    // Find the nearest to what the original was?
                                }
                                else
                                {
                                    foreach (var indexOffset in shapeData)
                                    {
                                        meshShapeDataBlock.AddRange(BitConverter.GetBytes(indexOffset.Key));
                                        meshShapeDataBlock.AddRange(BitConverter.GetBytes(indexOffset.Value));
                                    }
                                }
                            }
                            else
                            {

                                foreach (var indexOffset in shapeData)
                                {
                                    meshShapeDataBlock.AddRange(BitConverter.GetBytes(indexOffset.Key));
                                    meshShapeDataBlock.AddRange(BitConverter.GetBytes(indexOffset.Value));
                                }
                            }
                        }
                    }

                    lodNumber++;
                }

                fullModelDataBlock.AddRange(meshShapeDataBlock);

                #endregion
            }

            #endregion

            // Bone Index Part
            #region Bone Index Part Data Block

            var boneIndexPartDataBlock = new List<byte>();

            var boneIndexPart = xivMdl.BoneIndexPart;

            boneIndexPartDataBlock.AddRange(BitConverter.GetBytes(boneIndexPart.BoneIndexCount));

            foreach (var boneIndex in boneIndexPart.BoneIndexList)
            {
                boneIndexPartDataBlock.AddRange(BitConverter.GetBytes(boneIndex));
            }

            fullModelDataBlock.AddRange(boneIndexPartDataBlock);

            #endregion

            // Padding 
            #region Padding Data Block

            var paddingDataBlock = new List<byte>();

            paddingDataBlock.Add(xivMdl.PaddingSize);
            paddingDataBlock.AddRange(xivMdl.PaddedBytes);

            fullModelDataBlock.AddRange(paddingDataBlock);

            #endregion

            // Bounding Box
            #region Bounding Box Data Block

            var boundingBoxDataBlock = new List<byte>();

            var boundingBox = xivMdl.BoundBox;

            foreach (var point in boundingBox.PointList)
            {
                boundingBoxDataBlock.AddRange(BitConverter.GetBytes(point.X));
                boundingBoxDataBlock.AddRange(BitConverter.GetBytes(point.Y));
                boundingBoxDataBlock.AddRange(BitConverter.GetBytes(point.Z));
                boundingBoxDataBlock.AddRange(BitConverter.GetBytes(point.W));
            }

            fullModelDataBlock.AddRange(boundingBoxDataBlock);

            #endregion

            // Bone Transform
            #region Bone Transform Data Block

            var boneTransformDataBlock = new List<byte>();

            foreach (var boneTransformData in xivMdl.BoneTransformDataList)
            {
                var transform0 = boneTransformData.Transform0;
                var transform1 = boneTransformData.Transform1;

                boneTransformDataBlock.AddRange(BitConverter.GetBytes(transform0.X));
                boneTransformDataBlock.AddRange(BitConverter.GetBytes(transform0.Y));
                boneTransformDataBlock.AddRange(BitConverter.GetBytes(transform0.Z));
                boneTransformDataBlock.AddRange(BitConverter.GetBytes(transform0.W));

                boneTransformDataBlock.AddRange(BitConverter.GetBytes(transform1.X));
                boneTransformDataBlock.AddRange(BitConverter.GetBytes(transform1.Y));
                boneTransformDataBlock.AddRange(BitConverter.GetBytes(transform1.Z));
                boneTransformDataBlock.AddRange(BitConverter.GetBytes(transform1.W));
            }

            fullModelDataBlock.AddRange(boneTransformDataBlock);

            #endregion

            // Data Compression
            #region Data Compression

            var compressedMDLData = new List<byte>();

            // Vertex Info Compression
            var compressedVertexInfo = IOUtil.Compressor(vertexInfoBlock.ToArray());
            compressedMDLData.AddRange(BitConverter.GetBytes(16));
            compressedMDLData.AddRange(BitConverter.GetBytes(0));
            compressedMDLData.AddRange(BitConverter.GetBytes(compressedVertexInfo.Length));
            compressedMDLData.AddRange(BitConverter.GetBytes(vertexInfoBlock.Count));
            compressedMDLData.AddRange(compressedVertexInfo);

            var padding = 128 - (compressedVertexInfo.Length + 16) % 128;
            compressedMDLData.AddRange(new byte[padding]);
            var compressedVertexInfoSize = compressedVertexInfo.Length + 16 + padding;

            // Model Data Compression
            var totalModelDataCompressedSize = 0;
            var compressedModelSizes = new List<int>();

            var modelDataPartCount = (int)Math.Ceiling(fullModelDataBlock.Count / 16000f);
            var modelDataPartCountsList = new List<int>(modelDataPartCount);
            var remainingDataSize = fullModelDataBlock.Count;

            for (var i = 0; i < modelDataPartCount; i++)
            {
                if (remainingDataSize >= 16000)
                {
                    modelDataPartCountsList.Add(16000);
                    remainingDataSize -= 16000;
                }
                else
                {
                    modelDataPartCountsList.Add(remainingDataSize);
                }
            }

            for (var i = 0; i < modelDataPartCount; i++)
            {
                var compressedModelData =
                    IOUtil.Compressor(fullModelDataBlock.GetRange(i * 16000, modelDataPartCountsList[i]).ToArray());

                compressedMDLData.AddRange(BitConverter.GetBytes(16));
                compressedMDLData.AddRange(BitConverter.GetBytes(0));
                compressedMDLData.AddRange(BitConverter.GetBytes(compressedModelData.Length));
                compressedMDLData.AddRange(BitConverter.GetBytes(modelDataPartCountsList[i]));
                compressedMDLData.AddRange(compressedModelData);

                padding = 128 - (compressedModelData.Length + 16) % 128;
                compressedMDLData.AddRange(new byte[padding]);

                totalModelDataCompressedSize += compressedModelData.Length + 16 + padding;
                compressedModelSizes.Add(compressedModelData.Length + 16 + padding);
            }
            #endregion

            // Vertex Data Block
            #region Vertex Data Block

            var vertexDataSectionList = new List<VertexDataSection>();
            var compressedMeshSizes = new List<int>();
            var compressedIndexSizes = new List<int>();

            lodNum = 0;
            foreach (var lod in xivMdl.LoDList)
            {
                var vertexDataSection = new VertexDataSection();
                var meshNum = 0;
                foreach (var meshData in lod.MeshDataList)
                {
                    // We only make changes to LoD 0
                    if (lodNum == 0)
                    {
                        var importData = importDataDictionary[meshNum];

                        // Because our imported data does not include mesh shape data, we must include it manually
                        if (xivMdl.HasShapeData)
                        {
                            if (meshData.ShapePositionsDictionary != null)
                            {
                                // We add the data from the mesh vertex data
                                foreach (var vertIndex in meshData.ShapePositionsDictionary.Keys)
                                {
                                    var position = meshData.VertexData.Positions[vertIndex];
                                    var boneWeights = meshData.VertexData.BoneWeights[vertIndex];
                                    var boneIndices = meshData.VertexData.BoneIndices[vertIndex];

                                    var posDataType = vertexInfoDict[0][VertexUsageType.Position];

                                    if (posDataType == VertexDataType.Half4)
                                    {
                                        var x = new Half(position.X);
                                        var y = new Half(position.Y);
                                        var z = new Half(position.Z);
                                        var w = new Half(1);

                                        importData.VertexData0.AddRange(BitConverter.GetBytes(x.RawValue));
                                        importData.VertexData0.AddRange(BitConverter.GetBytes(y.RawValue));
                                        importData.VertexData0.AddRange(BitConverter.GetBytes(z.RawValue));
                                        importData.VertexData0.AddRange(BitConverter.GetBytes(w.RawValue));
                                    }
                                    else
                                    {
                                        importData.VertexData0.AddRange(BitConverter.GetBytes(position.X));
                                        importData.VertexData0.AddRange(BitConverter.GetBytes(position.Y));
                                        importData.VertexData0.AddRange(BitConverter.GetBytes(position.Z));
                                    }

                                    foreach (var boneWeight in boneWeights)
                                    {
                                        importData.VertexData0.Add((byte)Math.Round(boneWeight * 255f));
                                    }

                                    importData.VertexData0.AddRange(boneIndices);


                                    var normal = meshData.VertexData.Normals[vertIndex];
                                    var binormal = meshData.VertexData.BiNormals[vertIndex];
                                    var color = meshData.VertexData.Colors[vertIndex];
                                    var textureCoordinates0 = meshData.VertexData.TextureCoordinates0[vertIndex];
                                    var textureCoordinates1 = meshData.VertexData.TextureCoordinates1[vertIndex];

                                    importData.VertexData1.AddRange(BitConverter.GetBytes(normal.X));
                                    importData.VertexData1.AddRange(BitConverter.GetBytes(normal.Y));
                                    importData.VertexData1.AddRange(BitConverter.GetBytes(normal.Z));

                                    importData.VertexData1.Add((byte)((Math.Abs(binormal.X) * 255 + 255) / 2));
                                    importData.VertexData1.Add((byte)((Math.Abs(binormal.Y) * 255 + 255) / 2));
                                    importData.VertexData1.Add((byte)((Math.Abs(binormal.Z) * 255 + 255) / 2));
                                    importData.VertexData1.Add(0);

                                    importData.VertexData1.Add(color.A);
                                    importData.VertexData1.Add(color.R);
                                    importData.VertexData1.Add(color.G);
                                    importData.VertexData1.Add(color.B);


                                    importData.VertexData1.AddRange(BitConverter.GetBytes(textureCoordinates0.X));
                                    importData.VertexData1.AddRange(BitConverter.GetBytes(textureCoordinates0.Y));

                                    importData.VertexData1.AddRange(BitConverter.GetBytes(textureCoordinates1.X));
                                    importData.VertexData1.AddRange(BitConverter.GetBytes(textureCoordinates1.Y));
                                }
                            }

                        }

                        vertexDataSection.VertexDataBlock.AddRange(importData.VertexData0);
                        vertexDataSection.VertexDataBlock.AddRange(importData.VertexData1);
                        vertexDataSection.IndexDataBlock.AddRange(importData.IndexData);

                        var indexPadding = (importData.IndexCount * 2) % 16;
                        if (indexPadding != 0)
                        {
                            vertexDataSection.IndexDataBlock.AddRange(new byte[16 - indexPadding]);
                        }
                    }
                    // All other LoDs
                    else
                    {
                        var vertexInfo = vertexInfoDict[lodNum];
                        var vertexData = GetVertexByteData(meshData.VertexData, itemType, vertexInfo);

                        vertexDataSection.VertexDataBlock.AddRange(vertexData.VertexData0);
                        vertexDataSection.VertexDataBlock.AddRange(vertexData.VertexData1);
                        vertexDataSection.IndexDataBlock.AddRange(vertexData.IndexData);

                        var indexPadding = (vertexData.IndexCount * 2) % 16;

                        if (indexPadding != 0)
                        {
                            vertexDataSection.IndexDataBlock.AddRange(new byte[16 - indexPadding]);
                        }
                    }

                    meshNum++;
                }

                // Vertex Compression
                vertexDataSection.VertexDataBlockPartCount =
                    (int)Math.Ceiling(vertexDataSection.VertexDataBlock.Count / 16000f);
                var vertexDataPartCounts = new List<int>(vertexDataSection.VertexDataBlockPartCount);
                var remainingVertexData = vertexDataSection.VertexDataBlock.Count;

                for (var i = 0; i < vertexDataSection.VertexDataBlockPartCount; i++)
                {
                    if (remainingVertexData >= 16000)
                    {
                        vertexDataPartCounts.Add(16000);
                        remainingVertexData -= 16000;
                    }
                    else
                    {
                        vertexDataPartCounts.Add(remainingVertexData);
                    }
                }

                for (var i = 0; i < vertexDataSection.VertexDataBlockPartCount; i++)
                {
                    var compressedVertexData = IOUtil.Compressor(vertexDataSection.VertexDataBlock
                        .GetRange(i * 16000, vertexDataPartCounts[i]).ToArray());

                    compressedMDLData.AddRange(BitConverter.GetBytes(16));
                    compressedMDLData.AddRange(BitConverter.GetBytes(0));
                    compressedMDLData.AddRange(BitConverter.GetBytes(compressedVertexData.Length));
                    compressedMDLData.AddRange(BitConverter.GetBytes(vertexDataPartCounts[i]));
                    compressedMDLData.AddRange(compressedVertexData);

                    var vertexPadding = 128 - (compressedVertexData.Length + 16) % 128;
                    compressedMDLData.AddRange(new byte[vertexPadding]);

                    vertexDataSection.CompressedVertexDataBlockSize +=
                        compressedVertexData.Length + 16 + vertexPadding;

                    compressedMeshSizes.Add(compressedVertexData.Length + 16 + vertexPadding);
                }

                // Index Compression
                vertexDataSection.IndexDataBlockPartCount =
                    (int)Math.Ceiling((vertexDataSection.IndexDataBlock.Count / 16000f));

                var indexDataPartCounts = new List<int>(vertexDataSection.IndexDataBlockPartCount);
                var remainingIndexData = vertexDataSection.IndexDataBlock.Count;

                for (var i = 0; i < vertexDataSection.IndexDataBlockPartCount; i++)
                {
                    if (remainingIndexData >= 16000)
                    {
                        indexDataPartCounts.Add(16000);
                        remainingIndexData -= 16000;
                    }
                    else
                    {
                        indexDataPartCounts.Add(remainingIndexData);
                    }
                }

                for (var i = 0; i < vertexDataSection.IndexDataBlockPartCount; i++)
                {
                    var compressedIndexData = IOUtil.Compressor(vertexDataSection.IndexDataBlock
                        .GetRange(i * 16000, indexDataPartCounts[i]).ToArray());

                    compressedMDLData.AddRange(BitConverter.GetBytes(16));
                    compressedMDLData.AddRange(BitConverter.GetBytes(0));
                    compressedMDLData.AddRange(BitConverter.GetBytes(compressedIndexData.Length));
                    compressedMDLData.AddRange(BitConverter.GetBytes(indexDataPartCounts[i]));
                    compressedMDLData.AddRange(compressedIndexData);

                    var indexPadding = 128 - (compressedIndexData.Length + 16) % 128;

                    compressedMDLData.AddRange(new byte[indexPadding]);

                    vertexDataSection.CompressedIndexDataBlockSize +=
                        compressedIndexData.Length + 16 + indexPadding;
                    compressedIndexSizes.Add(compressedIndexData.Length + 16 + indexPadding);
                }

                vertexDataSectionList.Add(vertexDataSection);

                lodNum++;
            }
            #endregion

            // Header Creation
            #region Header Creation

            var datHeader = new List<byte>();

            // This is the most common size of header for models
            var headerLength = 256;

            // If the data is large enough, the header length goes to the next larger size (add 128 bytes)
            if ((compressedMeshSizes.Count + modelDataPartCount + 3 + compressedIndexSizes.Count) > 24)
            {
                headerLength = 384;
            }

            // Header Length
            datHeader.AddRange(BitConverter.GetBytes(headerLength));
            // Data Type (models are type 3 data)
            datHeader.AddRange(BitConverter.GetBytes(3));
            // Uncompressed size of the mdl file (68 is the header size (64) + vertex info block padding (4))
            var uncompressedSize = vertexInfoBlock.Count + fullModelDataBlock.Count + 68;
            // Add the vertex and index data block sizes to the uncomrpessed size
            foreach (var vertexDataSection in vertexDataSectionList)
            {
                uncompressedSize += vertexDataSection.VertexDataBlock.Count + vertexDataSection.IndexDataBlock.Count;
            }

            datHeader.AddRange(BitConverter.GetBytes(uncompressedSize));

            // Max Buffer Size?
            datHeader.AddRange(BitConverter.GetBytes(compressedMDLData.Count / 128 + 16));
            // Buffer Size
            datHeader.AddRange(BitConverter.GetBytes(compressedMDLData.Count / 128));
            // Block count
            datHeader.AddRange(BitConverter.GetBytes((short)5));
            // Unknown
            datHeader.AddRange(BitConverter.GetBytes((short)256));

            // Vertex Info Block Uncompressed
            var datPadding = 128 - vertexInfoBlock.Count % 128;
            datPadding = datPadding == 128 ? 0 : datPadding;
            datHeader.AddRange(BitConverter.GetBytes(vertexInfoBlock.Count + datPadding));
            // Model Data Block Uncompressed
            datPadding = 128 - fullModelDataBlock.Count % 128;
            datPadding = datPadding == 128 ? 0 : datPadding;
            datHeader.AddRange(BitConverter.GetBytes(fullModelDataBlock.Count + datPadding));
            // Vertex Data Block LoD[0] Uncompressed
            datPadding = 128 - vertexDataSectionList[0].VertexDataBlock.Count % 128;
            datPadding = datPadding == 128 ? 0 : datPadding;
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[0].VertexDataBlock.Count + datPadding));
            // Vertex Data Block LoD[1] Uncompressed
            datPadding = 128 - vertexDataSectionList[1].VertexDataBlock.Count % 128;
            datPadding = datPadding == 128 ? 0 : datPadding;
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[1].VertexDataBlock.Count + datPadding));
            // Vertex Data Block LoD[2] Uncompressed
            datPadding = 128 - vertexDataSectionList[2].VertexDataBlock.Count % 128;
            datPadding = datPadding == 128 ? 0 : datPadding;
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[2].VertexDataBlock.Count + datPadding));
            // Blank 1
            datHeader.AddRange(BitConverter.GetBytes(0));
            // Blank 2
            datHeader.AddRange(BitConverter.GetBytes(0));
            // Blank 3
            datHeader.AddRange(BitConverter.GetBytes(0));
            // Index Data Block LoD[0] Uncompressed
            datPadding = 128 - vertexDataSectionList[0].IndexDataBlock.Count % 128;
            datPadding = datPadding == 128 ? 0 : datPadding;
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[0].IndexDataBlock.Count + datPadding));
            // Index Data Block LoD[1] Uncompressed
            datPadding = 128 - vertexDataSectionList[1].IndexDataBlock.Count % 128;
            datPadding = datPadding == 128 ? 0 : datPadding;
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[1].IndexDataBlock.Count + datPadding));
            // Index Data Block LoD[2] Uncompressed
            datPadding = 128 - vertexDataSectionList[2].IndexDataBlock.Count % 128;
            datPadding = datPadding == 128 ? 0 : datPadding;
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[2].IndexDataBlock.Count + datPadding));

            // Vertex Info Block Compressed
            datHeader.AddRange(BitConverter.GetBytes(compressedVertexInfoSize));
            // Model Data Block Compressed
            datHeader.AddRange(BitConverter.GetBytes(totalModelDataCompressedSize));
            // Vertex Data Block LoD[0] Compressed
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[0].CompressedVertexDataBlockSize));
            // Vertex Data Block LoD[1] Compressed
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[1].CompressedVertexDataBlockSize));
            // Vertex Data Block LoD[2] Compressed
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[2].CompressedVertexDataBlockSize));
            // Blank 1
            datHeader.AddRange(BitConverter.GetBytes(0));
            // Blank 2
            datHeader.AddRange(BitConverter.GetBytes(0));
            // Blank 3
            datHeader.AddRange(BitConverter.GetBytes(0));
            // Index Data Block LoD[0] Compressed
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[0].CompressedIndexDataBlockSize));
            // Index Data Block LoD[1] Compressed
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[1].CompressedIndexDataBlockSize));
            // Index Data Block LoD[2] Compressed
            datHeader.AddRange(BitConverter.GetBytes(vertexDataSectionList[2].CompressedIndexDataBlockSize));

            var vertexInfoOffset = 0;
            var modelDataOffset = compressedVertexInfoSize;
            var vertexDataBlock1Offset = modelDataOffset + totalModelDataCompressedSize;
            var indexDataBlock1Offset = vertexDataBlock1Offset + vertexDataSectionList[0].CompressedVertexDataBlockSize;
            var vertexDataBlock2Offset = indexDataBlock1Offset + vertexDataSectionList[0].CompressedIndexDataBlockSize;
            var indexDataBlock2Offset = vertexDataBlock2Offset + vertexDataSectionList[1].CompressedVertexDataBlockSize;
            var vertexDataBlock3Offset = indexDataBlock2Offset + vertexDataSectionList[1].CompressedIndexDataBlockSize;
            var indexDataBlock3Offset = vertexDataBlock3Offset + vertexDataSectionList[2].CompressedVertexDataBlockSize;

            // Vertex Info Offset
            datHeader.AddRange(BitConverter.GetBytes(vertexInfoOffset));
            // Model Data Offset
            datHeader.AddRange(BitConverter.GetBytes(modelDataOffset));
            // Vertex Data Block LoD[0] Offset
            datHeader.AddRange(BitConverter.GetBytes(vertexDataBlock1Offset));
            // Vertex Data Block LoD[1] Offset
            datHeader.AddRange(BitConverter.GetBytes(vertexDataBlock2Offset));
            // Vertex Data Block LoD[2] Offset
            datHeader.AddRange(BitConverter.GetBytes(vertexDataBlock3Offset));
            // Blank 1
            datHeader.AddRange(BitConverter.GetBytes(0));
            // Blank 2
            datHeader.AddRange(BitConverter.GetBytes(0));
            // Blank 3
            datHeader.AddRange(BitConverter.GetBytes(0));
            // Index Data Block LoD[0] Offset
            datHeader.AddRange(BitConverter.GetBytes(indexDataBlock1Offset));
            // Index Data Block LoD[1] Offset
            datHeader.AddRange(BitConverter.GetBytes(indexDataBlock2Offset));
            // Index Data Block LoD[2] Offset
            datHeader.AddRange(BitConverter.GetBytes(indexDataBlock3Offset));

            var vertexDataBlock1 = 1 + modelDataPartCount;
            var indexDataBlock1 = vertexDataBlock1 + vertexDataSectionList[0].VertexDataBlockPartCount;
            var vertexDataBlock2 = indexDataBlock1 + vertexDataSectionList[0].IndexDataBlockPartCount;
            var indexDataBlock2 = vertexDataBlock2 + vertexDataSectionList[1].VertexDataBlockPartCount;
            var vertexDataBlock3 = indexDataBlock2 + 1;
            var indexDataBlock3 = vertexDataBlock3 + vertexDataSectionList[2].VertexDataBlockPartCount;

            // Vertex Info Index
            datHeader.AddRange(BitConverter.GetBytes((short)0));
            // Model Data Index
            datHeader.AddRange(BitConverter.GetBytes((short)1));
            // Vertex Data Block LoD[0] Index
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataBlock1));
            // Vertex Data Block LoD[1] Index
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataBlock2));
            // Vertex Data Block LoD[2] Index
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataBlock3));
            // Blank 1 (Copies Indices?)
            datHeader.AddRange(BitConverter.GetBytes((short)indexDataBlock1));
            // Blank 2 (Copies Indices?)
            datHeader.AddRange(BitConverter.GetBytes((short)indexDataBlock2));
            // Blank 3 (Copies Indices?)
            datHeader.AddRange(BitConverter.GetBytes((short)indexDataBlock3));
            // Index Data Block LoD[0] Index
            datHeader.AddRange(BitConverter.GetBytes((short)indexDataBlock1));
            // Index Data Block LoD[1] Index
            datHeader.AddRange(BitConverter.GetBytes((short)indexDataBlock2));
            // Index Data Block LoD[2] Index
            datHeader.AddRange(BitConverter.GetBytes((short)indexDataBlock3));

            // Vertex Info Part Count
            datHeader.AddRange(BitConverter.GetBytes((short)1));
            // Model Data Part Count
            datHeader.AddRange(BitConverter.GetBytes((short)modelDataPartCount));
            // Vertex Data Block LoD[0] part count
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataSectionList[0].VertexDataBlockPartCount));
            // Vertex Data Block LoD[1] part count
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataSectionList[1].VertexDataBlockPartCount));
            // Vertex Data Block LoD[2] part count
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataSectionList[2].VertexDataBlockPartCount));
            // Blank 1
            datHeader.AddRange(BitConverter.GetBytes((short)0));
            // Blank 2
            datHeader.AddRange(BitConverter.GetBytes((short)0));
            // Blank 3
            datHeader.AddRange(BitConverter.GetBytes((short)0));
            // Index Data Block LoD[0] part count
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataSectionList[0].IndexDataBlockPartCount));
            // Index Data Block LoD[1] part count
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataSectionList[1].IndexDataBlockPartCount));
            // Index Data Block LoD[2] part count
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataSectionList[2].IndexDataBlockPartCount));

            // Mesh Count
            datHeader.AddRange(BitConverter.GetBytes((short)modelData.MeshCount));
            // Material Count
            datHeader.AddRange(BitConverter.GetBytes((short)modelData.MaterialCount));
            // Unknown 1
            datHeader.AddRange(BitConverter.GetBytes((short)259));
            // Unknown 2
            datHeader.AddRange(BitConverter.GetBytes((short)0));

            var vertexDataBlockCount = 0;
            // Vertex Info Padded Size
            datHeader.AddRange(BitConverter.GetBytes((short)compressedVertexInfoSize));
            // Model Data Padded Size
            for (var i = 0; i < modelDataPartCount; i++)
            {
                datHeader.AddRange(BitConverter.GetBytes((short)compressedModelSizes[i]));
            }

            // Vertex Data Block LoD[0] part padded sizes
            for (var i = 0; i < vertexDataSectionList[0].VertexDataBlockPartCount; i++)
            {
                datHeader.AddRange(BitConverter.GetBytes((short)compressedMeshSizes[i]));
            }

            vertexDataBlockCount += vertexDataSectionList[0].VertexDataBlockPartCount;

            // Index Data Block LoD[0] padded size
            for (var i = 0; i < vertexDataSectionList[0].IndexDataBlockPartCount; i++)
            {
               datHeader.AddRange(BitConverter.GetBytes((short)compressedIndexSizes[i])); 
            }

            // Vertex Data Block LoD[1] part padded sizes
            for (var i = 0; i < vertexDataSectionList[1].VertexDataBlockPartCount; i++)
            {
                datHeader.AddRange(BitConverter.GetBytes((short)compressedMeshSizes[vertexDataBlockCount + i]));
            }

            vertexDataBlockCount += vertexDataSectionList[1].VertexDataBlockPartCount;

            // Index Data Block LoD[1] padded size
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataSectionList[1].CompressedIndexDataBlockSize));

            // Vertex Data Block LoD[2] part padded sizes
            for (var i = 0; i < vertexDataSectionList[2].VertexDataBlockPartCount; i++)
            {
                datHeader.AddRange(BitConverter.GetBytes((short)compressedMeshSizes[vertexDataBlockCount + i]));
            }

            // Index Data Block LoD[2] padded size
            datHeader.AddRange(BitConverter.GetBytes((short)vertexDataSectionList[2].CompressedIndexDataBlockSize));

            // Rest of Header
            if (datHeader.Count != 256 && datHeader.Count != 384)
            {
                var headerEnd = headerLength - datHeader.Count % headerLength;
                datHeader.AddRange(new byte[headerEnd]);
            }

            // Add the header to the MDL data
            compressedMDLData.InsertRange(0, datHeader);

            var dat = new Dat(_gameDirectory);

            var filePath = Path.Combine(xivMdl.MdlPath.Folder, xivMdl.MdlPath.File);

            if (rawDataOnly)
            {
                MDLRawData = compressedMDLData.ToArray();
            }
            else
            {
                dat.WriteToDat(compressedMDLData, modEntry, filePath, item.ItemCategory, item.Name, _dataFile, source, 3);
            }

            #endregion
        }

        /// <summary>
        /// Gets the import data in byte format
        /// </summary>
        /// <param name="colladaMeshDataList">The list of mesh data obtained from the imported collada file</param>
        /// <param name="itemType">The item type</param>
        /// <returns>A dictionary containing the vertex byte data per mesh</returns>
        private Dictionary<int, VertexByteData> GetImportData(List<ColladaMeshData> colladaMeshDataList, XivItemType itemType, Dictionary<int, Dictionary<VertexUsageType, VertexDataType>> vertexInfoDict)
        {
            var importDataDictionary = new Dictionary<int, VertexByteData>();

            var meshNumber = 0;
            foreach (var colladaMeshData in colladaMeshDataList)
            {
                var meshGeometry = colladaMeshData.MeshGeometry;

                var importData = new VertexByteData()
                {
                    VertexData0 = new List<byte>(),
                    VertexData1 = new List<byte>(),
                    IndexData = new List<byte>(),
                    VertexCount = meshGeometry.Positions.Count,
                    IndexCount = meshGeometry.Indices.Count
                };

                // Add the first vertex data set to the ImportData list
                // This contains [ Position, Bone Weights, Bone Indices]
                for (var i = 0; i < meshGeometry.Positions.Count; i++)
                {
                    // Positions for Weapon and Monster item types are half precision floating points
                    var posDataType = vertexInfoDict[0][VertexUsageType.Position];

                    if (posDataType == VertexDataType.Half4)
                    {
                        var x = new Half(meshGeometry.Positions[i].X);
                        var y = new Half(meshGeometry.Positions[i].Y);
                        var z = new Half(meshGeometry.Positions[i].Z);

                        importData.VertexData0.AddRange(BitConverter.GetBytes(x.RawValue));
                        importData.VertexData0.AddRange(BitConverter.GetBytes(y.RawValue));
                        importData.VertexData0.AddRange(BitConverter.GetBytes(z.RawValue));

                        // Half float positions have a W coordinate but it is never used and is defaulted to 1.
                        var w = new Half(1);
                        importData.VertexData0.AddRange(BitConverter.GetBytes(w.RawValue));

                    }
                    // Everything else has positions as singles 
                    else
                    {
                        importData.VertexData0.AddRange(BitConverter.GetBytes(meshGeometry.Positions[i].X));
                        importData.VertexData0.AddRange(BitConverter.GetBytes(meshGeometry.Positions[i].Y));
                        importData.VertexData0.AddRange(BitConverter.GetBytes(meshGeometry.Positions[i].Z));
                    }

                    // Furniture items do not have bone data
                    if (itemType != XivItemType.furniture)
                    {
                        // Bone Weights
                        importData.VertexData0.AddRange(colladaMeshData.BoneWeights[i]);

                        // Bone Indices
                        importData.VertexData0.AddRange(colladaMeshData.BoneIndices[i]);
                    }
                }

                // Add the second vertex data set to the ImportData list
                // This contains [ Normal, BiNormal, Color, Texture Coordinates ]
                for (var i = 0; i < meshGeometry.Normals.Count; i++)
                {
                    // Normals
                    var x = meshGeometry.Normals[i].X;
                    var y = meshGeometry.Normals[i].Y;
                    var z = meshGeometry.Normals[i].Z;

                    importData.VertexData1.AddRange(BitConverter.GetBytes(x));
                    importData.VertexData1.AddRange(BitConverter.GetBytes(y));
                    importData.VertexData1.AddRange(BitConverter.GetBytes(z));

                    // BiNormals
                    // Change the BiNormals based on Handedness
                    var biNormal = meshGeometry.BiTangents[i];
                    var handedness = colladaMeshData.Handedness[i];
                    if (handedness > 0)
                    {
                        biNormal = Vector3.Normalize(-biNormal);
                    }

                    if (biNormal.X < 0)
                    {
                        importData.VertexData1.Add((byte)((Math.Abs(biNormal.X) * 255 + 255) / 2));
                    }
                    else
                    {
                        importData.VertexData1.Add((byte)((-Math.Abs(biNormal.X) - .014) * 255 / 2 - 255 / 2));
                    }

                    if (biNormal.Y < 0)
                    {
                        importData.VertexData1.Add((byte)((Math.Abs(biNormal.Y) * 255 + 255) / 2));
                    }
                    else
                    {
                        importData.VertexData1.Add((byte)((-Math.Abs(biNormal.Y) - .014) * 255 / 2 - 255 / 2));
                    }

                    if (biNormal.Z < 0)
                    {
                        importData.VertexData1.Add((byte)((Math.Abs(biNormal.Z) * 255 + 255) / 2));
                    }
                    else
                    {
                        importData.VertexData1.Add((byte)((-Math.Abs(biNormal.Z) - .014) * 255 / 2 - 255 / 2));
                    }

                    // The W coordinate of BiNormals reflects its handedness
                    var w = handedness == 1 ? 255 : 0;

                    importData.VertexData1.Add((byte)w);

                    // Tangents
                    if (vertexInfoDict[0].ContainsKey(VertexUsageType.Tangent))
                    {
                        var tangent = meshGeometry.Tangents[i];
                        var tangentHandedness = 0;

                        if (tangentHandedness > 0)
                        {
                            tangent = Vector3.Normalize(-tangent);
                        }

                        if (tangent.X < 0)
                        {
                            importData.VertexData1.Add((byte)((Math.Abs(tangent.X) * 255 + 255) / 2));
                        }
                        else
                        {
                            importData.VertexData1.Add((byte)((-Math.Abs(tangent.X) - .014) * 255 / 2 - 255 / 2));
                        }

                        if (tangent.Y < 0)
                        {
                            importData.VertexData1.Add((byte)((Math.Abs(tangent.Y) * 255 + 255) / 2));
                        }
                        else
                        {
                            importData.VertexData1.Add((byte)((-Math.Abs(tangent.Y) - .014) * 255 / 2 - 255 / 2));
                        }

                        if (tangent.Z < 0)
                        {
                            importData.VertexData1.Add((byte)((Math.Abs(tangent.Z) * 255 + 255) / 2));
                        }
                        else
                        {
                            importData.VertexData1.Add((byte)((-Math.Abs(tangent.Z) - .014) * 255 / 2 - 255 / 2));
                        }

                        // The W coordinate of BiNormals reflects its handedness
                        w = tangentHandedness == 1 ? 255 : 0;

                        importData.VertexData1.Add((byte)w);
                    }

                    // Vertex Color
                    // The Vertex Color is currently not taken into consideration and is defaulted to 0xFFFFFFFF
                    if (vertexInfoDict[0].ContainsKey(VertexUsageType.Color))
                    {
                        importData.VertexData1.AddRange(BitConverter.GetBytes(0xFFFFFFFF));
                    }

                    // Texture Coordinates
                    var texCoordDataType = vertexInfoDict[0][VertexUsageType.TextureCoordinate];

                    var tcX = meshGeometry.TextureCoordinates[i].X;
                    var tcY = meshGeometry.TextureCoordinates[i].Y * -1f;
                    var tcZ = 0f;
                    var tcW = 0f;

                    // If secondary texture coordinates exist, use those instead of the default of 0
                    if (colladaMeshData.TextureCoordintes1.Count > 0)
                    {
                        tcZ = colladaMeshData.TextureCoordintes1[i].X;
                        tcW = colladaMeshData.TextureCoordintes1[i].Y * -1f;
                    }

                    importData.VertexData1.AddRange(BitConverter.GetBytes(tcX));
                    importData.VertexData1.AddRange(BitConverter.GetBytes(tcY));

                    if (texCoordDataType == VertexDataType.Float4)
                    {
                        importData.VertexData1.AddRange(BitConverter.GetBytes(tcZ));
                        importData.VertexData1.AddRange(BitConverter.GetBytes(tcW));
                    }
                }

                // Indices
                foreach (var index in meshGeometry.Indices)
                {
                    importData.IndexData.AddRange(BitConverter.GetBytes((short)index));
                }

                // Add the import data to the dictionary
                importDataDictionary.Add(meshNumber, importData);
                meshNumber++;
            }

            return importDataDictionary;
        }

        /// <summary>
        /// Get the vertex data in byte format
        /// </summary>
        /// <param name="vertexData">The vertex data to convert</param>
        /// <param name="itemType">The item type</param>
        /// <returns>A class containing the byte data for the given data</returns>
        private static VertexByteData GetVertexByteData(VertexData vertexData, XivItemType itemType, Dictionary<VertexUsageType, VertexDataType> vertexInfoDict)
        {
            var vertexByteData = new VertexByteData
            {
                VertexCount = vertexData.Positions.Count,
                IndexCount = vertexData.Indices.Count
            };

            for (var i = 0; i < vertexData.Positions.Count; i++)
            {
                if (vertexInfoDict[VertexUsageType.Position] == VertexDataType.Half4)
                {
                    var x = new Half(vertexData.Positions[i].X);
                    var y = new Half(vertexData.Positions[i].Y);
                    var z = new Half(vertexData.Positions[i].Z);

                    vertexByteData.VertexData0.AddRange(BitConverter.GetBytes(x.RawValue));
                    vertexByteData.VertexData0.AddRange(BitConverter.GetBytes(y.RawValue));
                    vertexByteData.VertexData0.AddRange(BitConverter.GetBytes(z.RawValue));

                    // Half float positions have a W coordinate but it is never used and is defaulted to 1.
                    var w = new Half(1.0f);
                    vertexByteData.VertexData0.AddRange(BitConverter.GetBytes(w.RawValue));
                }
                // If positions are not Half values, they are single values
                else
                {
                    vertexByteData.VertexData0.AddRange(BitConverter.GetBytes(vertexData.Positions[i].X));
                    vertexByteData.VertexData0.AddRange(BitConverter.GetBytes(vertexData.Positions[i].Y));
                    vertexByteData.VertexData0.AddRange(BitConverter.GetBytes(vertexData.Positions[i].Z));
                }

                // Furniture does not have bone data
                if (itemType != XivItemType.furniture)
                {
                    // Bone Weights
                    foreach (var boneWeight in vertexData.BoneWeights[i])
                    {
                        vertexByteData.VertexData0.Add((byte)Math.Round(boneWeight * 255f));
                    }

                    // Bone Indices
                    vertexByteData.VertexData0.AddRange(vertexData.BoneIndices[i]);
                }
            }

            for (var i = 0; i < vertexData.Normals.Count; i++)
            {
                // Normals
                var x = new Half(vertexData.Normals[i].X);
                var y = new Half(vertexData.Normals[i].Y);
                var z = new Half(vertexData.Normals[i].Z);
                var w = new Half(0);

                vertexByteData.VertexData1.AddRange(BitConverter.GetBytes(x.RawValue));
                vertexByteData.VertexData1.AddRange(BitConverter.GetBytes(y.RawValue));
                vertexByteData.VertexData1.AddRange(BitConverter.GetBytes(z.RawValue));
                vertexByteData.VertexData1.AddRange(BitConverter.GetBytes(w.RawValue));

                // BiNormals
                vertexByteData.VertexData1.Add((byte)((Math.Abs(vertexData.BiNormals[i].X) * 255 + 255) / 2));
                vertexByteData.VertexData1.Add((byte)((Math.Abs(vertexData.BiNormals[i].Y) * 255 + 255) / 2));
                vertexByteData.VertexData1.Add((byte)((Math.Abs(vertexData.BiNormals[i].Z) * 255 + 255) / 2));
                vertexByteData.VertexData1.Add(vertexData.BiNormalHandedness[i]);

                // Tangents
                if (vertexInfoDict.ContainsKey(VertexUsageType.Tangent))
                {
                    vertexByteData.VertexData1.Add((byte)((Math.Abs(vertexData.Tangents[i].X) * 255 + 255) / 2));
                    vertexByteData.VertexData1.Add((byte)((Math.Abs(vertexData.Tangents[i].Y) * 255 + 255) / 2));
                    vertexByteData.VertexData1.Add((byte)((Math.Abs(vertexData.Tangents[i].Z) * 255 + 255) / 2));
                    vertexByteData.VertexData1.Add(vertexData.BiNormalHandedness[i]);
                }

                // Colors
                if (vertexData.Colors.Count > 0)
                {
                    var colorVector = vertexData.Colors[i].ToVector4();

                    vertexByteData.VertexData1.Add((byte)(colorVector.W * 255));
                    vertexByteData.VertexData1.Add((byte)(colorVector.X * 255));
                    vertexByteData.VertexData1.Add((byte)(colorVector.Y * 255));
                    vertexByteData.VertexData1.Add((byte)(colorVector.Z * 255));
                }

                // Texture Coordinates
                var tc0x = new Half(vertexData.TextureCoordinates0[i].X);
                var tc0y = new Half(vertexData.TextureCoordinates0[i].Y);

                vertexByteData.VertexData1.AddRange(BitConverter.GetBytes(tc0x.RawValue));
                vertexByteData.VertexData1.AddRange(BitConverter.GetBytes(tc0y.RawValue));

                if (vertexData.TextureCoordinates1.Count > 0)
                {
                    var tc1x = new Half(vertexData.TextureCoordinates1[i].X);
                    var tc1y = new Half(vertexData.TextureCoordinates1[i].Y);

                    vertexByteData.VertexData1.AddRange(BitConverter.GetBytes(tc1x.RawValue));
                    vertexByteData.VertexData1.AddRange(BitConverter.GetBytes(tc1y.RawValue));
                }
            }

            // Indices
            foreach (var index in vertexData.Indices)
            {
                vertexByteData.IndexData.AddRange(BitConverter.GetBytes((short)index));
            }

            return vertexByteData;
        }

        /// <summary>
        /// Gets the MDL path
        /// </summary>
        /// <param name="itemModel">The item model</param>
        /// <param name="xivRace">The selected race for the given item</param>
        /// <param name="itemType">The items type</param>
        /// <param name="secondaryModel">The secondary model if any</param>
        /// <returns>A Tuple containing the Folder and File string paths</returns>
        private (string Folder, string File) GetMdlPath(IItemModel itemModel, XivRace xivRace, XivItemType itemType, XivModelInfo secondaryModel, string mdlStringPath = null)
        {
            if (mdlStringPath != null)
            {
                var folder = Path.GetDirectoryName(mdlStringPath).Replace("\\", "/");
                var file = Path.GetFileName(mdlStringPath);

                return (folder, file);
            }

            string mdlFolder = "", mdlFile = "";

            var id = itemModel.ModelInfo.ModelID.ToString().PadLeft(4, '0');
            if (secondaryModel != null)
            {
                id = secondaryModel.ModelID.ToString().PadLeft(4, '0');
            }
            var bodyVer = itemModel.ModelInfo.Body.ToString().PadLeft(4, '0');
            var race = xivRace.GetRaceCode();

            switch (itemType)
            {
                case XivItemType.equipment:
                    mdlFolder = $"chara/{itemType}/e{id}/model";
                    mdlFile   = $"c{race}e{id}_{SlotAbbreviationDictionary[itemModel.ItemCategory]}{MdlExtension}";
                    break;
                case XivItemType.accessory:
                    mdlFolder = $"chara/{itemType}/a{id}/model";
                    mdlFile   = $"c{race}a{id}_{SlotAbbreviationDictionary[itemModel.ItemCategory]}{MdlExtension}";
                    break;
                case XivItemType.weapon:
                    mdlFolder = $"chara/{itemType}/w{id}/obj/body/b{bodyVer}/model";
                    mdlFile   = $"w{id}b{bodyVer}{MdlExtension}";
                    break;
                case XivItemType.monster:
                    mdlFolder = $"chara/{itemType}/m{id}/obj/body/b{bodyVer}/model";
                    mdlFile   = $"m{id}b{bodyVer}{MdlExtension}";
                    break;
                case XivItemType.demihuman:
                    mdlFolder = $"chara/{itemType}/d{id}/obj/equipment/e{bodyVer}/model";
                    mdlFile   = $"d{id}e{bodyVer}_{SlotAbbreviationDictionary[itemModel.ItemSubCategory]}{MdlExtension}";
                    break;
                case XivItemType.human:
                    if (itemModel.ItemCategory.Equals(XivStrings.Body))
                    {
                        mdlFolder = $"chara/{itemType}/c{race}/obj/body/b{bodyVer}/model";
                        mdlFile   = $"c{race}b{bodyVer}_{SlotAbbreviationDictionary[itemModel.ItemSubCategory]}{MdlExtension}";
                    }
                    else if (itemModel.ItemCategory.Equals(XivStrings.Hair))
                    {
                        mdlFolder = $"chara/{itemType}/c{race}/obj/hair/h{bodyVer}/model";
                        mdlFile   = $"c{race}h{bodyVer}_{SlotAbbreviationDictionary[itemModel.ItemCategory]}{MdlExtension}";
                    }
                    else if (itemModel.ItemCategory.Equals(XivStrings.Face))
                    {
                        mdlFolder = $"chara/{itemType}/c{race}/obj/face/f{bodyVer}/model";
                        mdlFile   = $"c{race}f{bodyVer}_{SlotAbbreviationDictionary[itemModel.ItemCategory]}{MdlExtension}";
                    }
                    else if (itemModel.ItemCategory.Equals(XivStrings.Tail))
                    {
                        mdlFolder = $"chara/{itemType}/c{race}/obj/tail/t{bodyVer}/model";
                        mdlFile   = $"c{race}t{bodyVer}_{SlotAbbreviationDictionary[itemModel.ItemCategory]}{MdlExtension}";
                    }
                    break;
                case XivItemType.furniture:
                    var part = "";
                    if (itemModel.ItemSubCategory != "base")
                    {
                        part = itemModel.ItemSubCategory;
                    }

                    if (itemModel.ItemCategory.Equals(XivStrings.Furniture_Indoor))
                    {
                        mdlFolder = $"bgcommon/hou/indoor/general/{id}/bgparts";
                        mdlFile = $"fun_b0_m{id}{part}{MdlExtension}";
                    }
                    else if (itemModel.ItemCategory.Equals(XivStrings.Furniture_Outdoor))
                    {
                        mdlFolder = $"bgcommon/hou/outdoor/general/{id}/bgparts";
                        mdlFile = $"gar_b0_m{id}{part}{MdlExtension}";
                    }

                    break;
                default:
                    mdlFolder = "";
                    mdlFile = "";
                    break;
            }

            return (mdlFolder, mdlFile);
        }

        private static readonly Dictionary<string, string> SlotAbbreviationDictionary = new Dictionary<string, string>
        {
            {XivStrings.Head, "met"},
            {XivStrings.Hands, "glv"},
            {XivStrings.Legs, "dwn"},
            {XivStrings.Feet, "sho"},
            {XivStrings.Body, "top"},
            {XivStrings.Ears, "ear"},
            {XivStrings.Neck, "nek"},
            {XivStrings.Rings, "rir"},
            {XivStrings.Wrists, "wrs"},
            {XivStrings.Head_Body, "top"},
            {XivStrings.Body_Hands, "top"},
            {XivStrings.Body_Hands_Legs, "top"},
            {XivStrings.Body_Legs_Feet, "top"},
            {XivStrings.Body_Hands_Legs_Feet, "top"},
            {XivStrings.Legs_Feet, "top"},
            {XivStrings.All, "top"},
            {XivStrings.Face, "fac"},
            {XivStrings.Iris, "iri"},
            {XivStrings.Etc, "etc"},
            {XivStrings.Accessory, "acc"},
            {XivStrings.Hair, "hir"},
            {XivStrings.Tail, "til"}

        };

        private static readonly Dictionary<byte, VertexDataType> VertexTypeDictionary =
            new Dictionary<byte, VertexDataType>
            {
                {0x0, VertexDataType.Float1},
                {0x1, VertexDataType.Float2},
                {0x2, VertexDataType.Float3},
                {0x3, VertexDataType.Float4},
                {0x5, VertexDataType.Ubyte4},
                {0x6, VertexDataType.Short2},
                {0x7, VertexDataType.Short4},
                {0x8, VertexDataType.Ubyte4n},
                {0x9, VertexDataType.Short2n},
                {0xA, VertexDataType.Short4n},
                {0xD, VertexDataType.Half2},
                {0xE, VertexDataType.Half4},
                {0xF, VertexDataType.Compress}
            };

        private static readonly Dictionary<byte, VertexUsageType> VertexUsageDictionary =
            new Dictionary<byte, VertexUsageType>
            {
                {0x0, VertexUsageType.Position },
                {0x1, VertexUsageType.BoneWeight },
                {0x2, VertexUsageType.BoneIndex },
                {0x3, VertexUsageType.Normal },
                {0x4, VertexUsageType.TextureCoordinate },
                {0x5, VertexUsageType.Tangent },
                {0x6, VertexUsageType.Binormal },
                {0x7, VertexUsageType.Color }
            };

        private class ColladaMeshData
        {
            public MeshGeometry3D MeshGeometry { get; set; }

            public List<byte[]> BoneIndices { get; set; }

            public List<byte[]> BoneWeights { get; set; }

            public List<int> Handedness { get; set; } = new List<int>();

            public Vector2Collection TextureCoordintes1 { get; set; }

            public Dictionary<int, int> PartsDictionary { get; set; }
        }

        /// <summary>
        /// This class holds the imported data after its been converted to bytes
        /// </summary>
        private class VertexByteData
        {
            public List<byte> VertexData0 { get; set; } = new List<byte>();

            public List<byte> VertexData1 { get; set; } = new List<byte>();

            public List<byte> IndexData { get; set; } = new List<byte>();

            public int VertexCount { get; set; }

            public int IndexCount { get; set; }
        }

        private class VertexDataSection
        {
            public int CompressedVertexDataBlockSize { get; set; }

            public int CompressedIndexDataBlockSize { get; set; }

            public int VertexDataBlockPartCount { get; set; }

            public int IndexDataBlockPartCount { get; set; }

            public List<byte> VertexDataBlock = new List<byte>();

            public List<byte> IndexDataBlock = new List<byte>();
        }
    }
}