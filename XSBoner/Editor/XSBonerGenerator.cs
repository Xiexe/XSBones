using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

public class XSBonerGenerator : EditorWindow
{
    private Object armatureObj;
    private Object bone;
    private Object smr;
    private Material boneMaterial;
    private Material ikMaterial;
    private bool haveIKLines;
    private bool spookMode;
    private Animator ani;

    private List<Transform> bones;
    private Hashtable bonesByHash;
    private List<BoneWeight> boneWeights;
    private List<CombineInstance> combineInstances;
    private List<Color> coloUrs;
    private Object startingBone;

    [MenuItem("Xiexe/Tools/XSBonerGenerator")]
    static void Init()
    {
        XSBonerGenerator window = (XSBonerGenerator)GetWindow(typeof(XSBonerGenerator));
        window.Show();
    }

    private void OnGUI()
    {
        armatureObj = EditorGUILayout.ObjectField(
            new GUIContent("Animator Object", "Your Model's Animator object"),
            armatureObj,
            typeof(Animator),
            true
        );

        if (armatureObj != null)
        {
            ani = (Animator)armatureObj;
        }
        else
        {
            startingBone = null;
            smr = null;
        }

        if (armatureObj && !ani.isHuman)
        {
            if (startingBone == null)
            {
                if (ani.transform.childCount > 0)
                {
                    startingBone = ani.transform.GetChild(0);
                    for (int i = 0; i < ani.transform.childCount; i++)
                    {
                        if (ani.transform.GetChild(i).childCount > 0)
                        {
                            startingBone = ani.transform.GetChild(i).GetChild(0);
                            break;
                        }
                    }
                }
            }

            startingBone = EditorGUILayout.ObjectField(
                new GUIContent("Starting Bone", "Where the bones start from"),
                startingBone,
                typeof(Transform),
                true
            );
        }
        else
        {
            startingBone = null;
        }

        bone = EditorGUILayout.ObjectField(
            new GUIContent("Bone Model", "The Model to use as the bone"),
            bone,
            typeof(Object),
            true
        );

        if (armatureObj != null && smr == null)
        {
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in ani.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skinnedMeshRenderer.transform.parent == ani.transform)
                {
                    smr = skinnedMeshRenderer;
                    break;
                }
            }
        }

        smr = EditorGUILayout.ObjectField(
            new GUIContent("Skinned Mesh Renderer", "The main skinned mesh renderer"),
            smr,
            typeof(SkinnedMeshRenderer),
            true
        );

        boneMaterial = (Material)EditorGUILayout.ObjectField(
            new GUIContent("Bone Material", "The Material you want for your bones"),
            boneMaterial,
            typeof(Material),
            true
        );

        if (armatureObj && ani.isHuman)
        {
            if (haveIKLines)
            {
                ikMaterial = (Material)EditorGUILayout.ObjectField(
                    new GUIContent("IK Material", "The Material you want for your IK Lines"),
                    ikMaterial,
                    typeof(Material),
                    true
                );
            }

            haveIKLines = EditorGUILayout.Toggle("Have IK Lines", haveIKLines);
        }
        else
        {
            haveIKLines = false;
        }

        spookMode = EditorGUILayout.Toggle("Spook Mode (Optional)", spookMode);

        bool error = false;

        if (armatureObj == null)
        {
            EditorGUILayout.HelpBox("No Animator found", MessageType.Error);
            error = true;
        }

        if (bone == null)
        {
            EditorGUILayout.HelpBox("No Bone Object found", MessageType.Error);
            error = true;
        }

        if (smr == null)
        {
            EditorGUILayout.HelpBox("No Skinned Mesh Renderer found", MessageType.Error);
            error = true;
        }

        if (boneMaterial == null)
        {
            EditorGUILayout.HelpBox("No Bone Material found", MessageType.Error);
            error = true;
        }

        if (ikMaterial == null && haveIKLines)
        {
            EditorGUILayout.HelpBox("No IK Material found", MessageType.Error);
            error = true;
        }

        if (error)
        {
            return;
        }

        EditorGUILayout.Separator();

        if (GUILayout.Button("Generate"))
        {
            string[] guids1 = AssetDatabase.FindAssets("XSBonerGenerator", null);
            string untouchedString = AssetDatabase.GUIDToAssetPath(guids1[0]);
            string[] splitString = untouchedString.Split('/');

            ArrayUtility.RemoveAt(ref splitString, splitString.Length - 1);
            ArrayUtility.RemoveAt(ref splitString, splitString.Length - 1);

            string finalFilePath = string.Join("/", splitString);
            string pathToGenerated = finalFilePath + "/Generated";
            string editorPath = string.Join("/", splitString) + "/Editor";

            if (!Directory.Exists(pathToGenerated))
            {
                Directory.CreateDirectory(pathToGenerated);
            }

            bone = AssetDatabase.LoadAssetAtPath(AssetDatabase.GetAssetPath(bone), typeof(Object));
            boneMaterial = (Material)AssetDatabase.LoadAssetAtPath(AssetDatabase.GetAssetPath(boneMaterial), typeof(Material));
            ikMaterial = (Material)AssetDatabase.LoadAssetAtPath(AssetDatabase.GetAssetPath(ikMaterial), typeof(Material));

            string name = Regex.Replace(ani.name, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);
            string bonename = Regex.Replace(bone.name, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);

            bones = new List<Transform>();
            bonesByHash = new Hashtable();
            boneWeights = new List<BoneWeight>();
            combineInstances = new List<CombineInstance>();
            coloUrs = new List<Color>();

            int boneIndex = 0;
            foreach (Transform currentBone in ((SkinnedMeshRenderer)smr).bones)
            {
                if (currentBone != null)
                {
                    bones.Add(currentBone);
                    bonesByHash.Add(currentBone.name, boneIndex);
                    boneIndex++;
                }
            }

            recursiveShit(
                startingBone != null ? (Transform)startingBone : ani.GetBoneTransform(HumanBodyBones.Hips),
                collectDynamicBones(ani.transform)
            );

            List<Matrix4x4> bindposes = new List<Matrix4x4>();
            for (int bindposeIndex = 0; bindposeIndex < bones.Count; bindposeIndex++)
            {
                bindposes.Add(bones[bindposeIndex].worldToLocalMatrix * ani.transform.worldToLocalMatrix);
            }

            GameObject yourBones = new GameObject(name + "_" + bonename + "_YourBones");
            yourBones.transform.parent = ani.transform;

            SkinnedMeshRenderer yourSkinnedMeshRenderer = yourBones.AddComponent<SkinnedMeshRenderer>();
            yourSkinnedMeshRenderer.sharedMesh = new Mesh
            {
                name = name + "_" + bonename + "_YourBones"
            };

            if (spookMode)
            {
                yourBones.AddComponent<AudioSource>();
                AudioSource doot = yourBones.GetComponent<AudioSource>();
                doot.clip = (AudioClip)AssetDatabase.LoadAssetAtPath(editorPath + "/Doot.mp3", typeof(AudioClip));
                doot.spatialBlend = 1;
                doot.dopplerLevel = 0;
                doot.minDistance = 2;
                doot.maxDistance = 10;
            }

            yourSkinnedMeshRenderer.sharedMesh.CombineMeshes(combineInstances.ToArray());

            Vector3 scale = ani.transform.localScale;

            List<Vector3> boneVertices = new List<Vector3>();
            for (int vertexIndex = 0; vertexIndex < yourSkinnedMeshRenderer.sharedMesh.vertexCount; vertexIndex++)
            {
                Vector3 vertex = yourSkinnedMeshRenderer.sharedMesh.vertices[vertexIndex];
                vertex.x *= scale.x;
                vertex.y *= scale.y;
                vertex.z *= scale.z;
                vertex += ani.transform.position;
                boneVertices.Add(vertex);
            }

            if (haveIKLines)
            {
                IKLines(boneVertices, HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm);
                IKLines(boneVertices, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm);
                IKLines(boneVertices, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg);
                IKLines(boneVertices, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg);
            }

            yourSkinnedMeshRenderer.sharedMesh.vertices = boneVertices.ToArray();

            if (haveIKLines)
            {
                yourSkinnedMeshRenderer.sharedMesh.subMeshCount = 2;
                int[] values = Enumerable.Range(yourSkinnedMeshRenderer.sharedMesh.vertexCount - 12, 12).ToArray();
                yourSkinnedMeshRenderer.sharedMesh.SetTriangles(values, 1);
            }

            yourSkinnedMeshRenderer.bones = bones.ToArray();
            yourSkinnedMeshRenderer.rootBone = bones[0];
            yourSkinnedMeshRenderer.sharedMesh.boneWeights = boneWeights.ToArray();
            yourSkinnedMeshRenderer.sharedMesh.bindposes = bindposes.ToArray();
            yourSkinnedMeshRenderer.sharedMesh.colors = coloUrs.ToArray();
            yourSkinnedMeshRenderer.sharedMaterials = haveIKLines
                ? new Material[] { boneMaterial, ikMaterial }
                : new Material[] { boneMaterial };

            yourSkinnedMeshRenderer.sharedMesh.RecalculateBounds();

            AssetDatabase.CreateAsset(
                yourSkinnedMeshRenderer.sharedMesh,
                pathToGenerated + "/" + name + "_" + bonename + "_YourBones.asset"
            );
            AssetDatabase.SaveAssets();

            armatureObj = null;
        }
    }

    private HashSet<Transform> collectDynamicBones(Transform transform)
    {
        HashSet<Transform> results = new HashSet<Transform>();
        collectDynamicBones(transform, results);
        return results;
    }

    private void collectDynamicBones(Transform transform, HashSet<Transform> results)
    {
        foreach (VRCPhysBone dynamicBone in transform.GetComponents<VRCPhysBone>())
        {
            collectDynamicBonesForOneScript(dynamicBone.GetRootTransform(), dynamicBone.ignoreTransforms, results);
        }

        for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
        {
            collectDynamicBones(transform.GetChild(childIndex), results);
        }
    }

    private void collectDynamicBonesForOneScript(Transform transform, List<Transform> exclusions, HashSet<Transform> results)
    {
        if (exclusions.Contains(transform))
        {
            return;
        }

        results.Add(transform);

        for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
        {
            collectDynamicBonesForOneScript(transform.GetChild(childIndex), exclusions, results);
        }
    }

    private void recursiveShit(Transform transform, HashSet<Transform> dynamicBones)
    {
        bool dynbone = dynamicBones.Contains(transform);

        if (transform.gameObject.activeInHierarchy && bones.Contains(transform))
        {
            bool hasActiveChildBone = false;

            for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                Transform childTransform = transform.GetChild(childIndex);

                if (!childTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                hasActiveChildBone = true;
                addCapColl(transform, childTransform, dynbone);

                recursiveShit(childTransform, dynamicBones);
            }

            if (!hasActiveChildBone)
            {
                addEndCapColl(transform, dynbone, 0);
            }
        }
    }

    private void addCapColl(Transform transform1, Transform transform2, bool dynbone)
    {
        GameObject boneSpawn = Instantiate(bone, transform1.position, transform1.rotation) as GameObject;
        float dist = Vector3.Distance(transform1.position, transform2.position) * 0.5f;

        boneSpawn.name = transform1.name + " -> " + transform2.name;
        boneSpawn.transform.localScale = new Vector3(dist, dist, dist);
        boneSpawn.transform.LookAt(transform2.position);
        boneSpawn.transform.rotation = Quaternion.Euler(
            boneSpawn.transform.rotation.eulerAngles + new Vector3(90f, 0f, 0f)
        );

        bool isHumanoid = false;
        for (int boneIndex = 0; boneIndex < (int)HumanBodyBones.LastBone; boneIndex++)
        {
            HumanBodyBones humanBoneId = (HumanBodyBones)boneIndex;
            Transform humanBoneTransform = ani.GetBoneTransform(humanBoneId);

            if (humanBoneTransform != null && humanBoneTransform == transform1)
            {
                isHumanoid = true;
                break;
            }
        }

        InsertSMRToCombine(boneSpawn.GetComponentInChildren<MeshFilter>(), transform1.name, isHumanoid, dynbone);
        DestroyImmediate(boneSpawn);
    }

    private void addEndCapColl(Transform transform, bool dynbone, float extensionLength)
    {
        GameObject boneSpawn = Instantiate(bone, transform.position, transform.rotation) as GameObject;

        Vector3 extensionDirection = transform.up;

        float previousBoneLength = extensionLength;
        if (transform.parent != null)
        {
            previousBoneLength = Vector3.Distance(transform.position, transform.parent.position);
        }

        Vector3 endPosition = transform.position + extensionDirection * previousBoneLength;

        boneSpawn.name = transform.name + " -> End";
        boneSpawn.transform.localScale = new Vector3(
            previousBoneLength * 0.5f,
            previousBoneLength * 0.5f,
            previousBoneLength * 0.5f
        );
        boneSpawn.transform.LookAt(endPosition);
        boneSpawn.transform.rotation = Quaternion.Euler(
            boneSpawn.transform.rotation.eulerAngles + new Vector3(90f, 0f, 0f)
        );

        bool isHumanoid = false;
        for (int boneIndex = 0; boneIndex < (int)HumanBodyBones.LastBone; boneIndex++)
        {
            HumanBodyBones humanBoneId = (HumanBodyBones)boneIndex;
            Transform humanBoneTransform = ani.GetBoneTransform(humanBoneId);

            if (humanBoneTransform != null && humanBoneTransform == transform)
            {
                isHumanoid = true;
                break;
            }
        }

        InsertSMRToCombine(boneSpawn.GetComponentInChildren<MeshFilter>(), transform.name, isHumanoid, dynbone);
        DestroyImmediate(boneSpawn);
    }

    private void InsertSMRToCombine(MeshFilter meshFilter, string bonename, bool hoomanoid, bool dynbone)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        if (bonesByHash[bonename] == null)
        {
            return;
        }

        int boneIndex = (int)bonesByHash[bonename];

        for (int vertexIndex = 0; vertexIndex < meshFilter.sharedMesh.vertexCount; vertexIndex++)
        {
            BoneWeight boneWeight = new BoneWeight
            {
                boneIndex0 = boneIndex,
                weight0 = 1f
            };

            boneWeights.Add(boneWeight);
        }

        CombineInstance combineInstance = new CombineInstance
        {
            mesh = meshFilter.sharedMesh,
            transform = meshFilter.transform.localToWorldMatrix
        };

        Color colour = new Color();
        if (dynbone)
        {
            colour = Color.blue;
        }
        else if (hoomanoid)
        {
            colour = Color.red;
        }

        for (int vertexIndex = 0; vertexIndex < meshFilter.sharedMesh.vertexCount; vertexIndex++)
        {
            coloUrs.Add(colour);
        }

        combineInstances.Add(combineInstance);
    }

    private void IKLines(List<Vector3> vertices, HumanBodyBones upper, HumanBodyBones joint, HumanBodyBones lower)
    {
        Transform upperT = ani.GetBoneTransform(upper);
        Transform jointT = ani.GetBoneTransform(joint);
        Transform lowerT = ani.GetBoneTransform(lower);

        if (upperT == null) return;
        if (jointT == null) return;
        if (lowerT == null) return;

        BoneWeight[] ikBoneWeights = new BoneWeight[3];
        ikBoneWeights[0].boneIndex0 = (int)bonesByHash[upperT.name];
        ikBoneWeights[0].weight0 = 1;
        ikBoneWeights[1].boneIndex0 = (int)bonesByHash[lowerT.name];
        ikBoneWeights[1].weight0 = 1;
        ikBoneWeights[2].boneIndex0 = (int)bonesByHash[upperT.name];
        ikBoneWeights[2].weight0 = 1;

        boneWeights.Add(ikBoneWeights[0]);
        boneWeights.Add(ikBoneWeights[1]);
        boneWeights.Add(ikBoneWeights[2]);

        Vector3 scale = ani.transform.localScale;

        Vector3 upperV = upperT.position;
        upperV.x *= scale.x;
        upperV.y *= scale.y;
        upperV.z *= scale.z;
        upperV += ani.transform.position;
        vertices.Add(upperV);

        Vector3 jointV = jointT.position;
        jointV.x *= scale.x;
        jointV.y *= scale.y;
        jointV.z *= scale.z;
        jointV += ani.transform.position;
        vertices.Add(jointV);

        vertices.Add(upperV);

        coloUrs.Add(Color.black);
        coloUrs.Add(Color.black);
        coloUrs.Add(Color.black);
    }
}