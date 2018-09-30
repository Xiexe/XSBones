using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;

public class XSBonerGenerator : EditorWindow {
    private Object armatureObj;
    private Object bone;
    private Object smr;
    private Material boneMaterial;
    private Material ikMaterial;
    private bool haveIKLines;
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
        armatureObj = EditorGUILayout.ObjectField(new GUIContent("Animator Object", "Your Model's Animator object"), armatureObj, typeof(Animator), true);
        if (armatureObj != null) {
            ani = (Animator)armatureObj;
        } else
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
            startingBone = EditorGUILayout.ObjectField(new GUIContent("Starting Bone", "Where the bones start from"), startingBone, typeof(Transform), true);
        } else
        {
            startingBone = null;
        }

        bone = EditorGUILayout.ObjectField(new GUIContent("Bone Model", "The Model to use as the bone"), bone, typeof(Object), true);
        if (armatureObj != null && smr == null)
        {
            foreach (SkinnedMeshRenderer skinedmeshr in ani.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skinedmeshr.transform.parent == ani.transform)
                {
                    smr = skinedmeshr;
                    break;
                }
            }
        }
        smr = EditorGUILayout.ObjectField(new GUIContent("Skinned Mesh Renderer", "The main skinned mesh renderer"), smr, typeof(SkinnedMeshRenderer), true);
        boneMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("Bone Material", "The Material you want for your bones"), boneMaterial, typeof(Material), true);

        if (armatureObj && ani.isHuman) {
            if (haveIKLines) {
                ikMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("IK Material", "The Material you want for your IK Lines"), ikMaterial, typeof(Material), true);
            }
            haveIKLines = EditorGUILayout.Toggle("Have IK Lines", haveIKLines);
        } else
        {
            haveIKLines = false;
        }

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

        if (error) return;

        EditorGUILayout.Separator();

        if (GUILayout.Button("Generate"))
        {

            string[] guids1 = AssetDatabase.FindAssets("XSBonerGenerator", null);
            string untouchedString = AssetDatabase.GUIDToAssetPath(guids1[0]);
            string[] splitString = untouchedString.Split('/');

            ArrayUtility.RemoveAt(ref splitString, splitString.Length - 1);
            ArrayUtility.RemoveAt(ref splitString, splitString.Length - 1);

            string finalFilePath = string.Join("/", splitString);
            finalFilePath += "/Generated";

            if (!Directory.Exists(finalFilePath)) {
                Directory.CreateDirectory(finalFilePath);
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
            foreach (Transform _bone in ((SkinnedMeshRenderer)smr).bones)
            {
                if (_bone != null) {
                    bones.Add(_bone);
                    bonesByHash.Add(_bone.name, boneIndex);
                    boneIndex++;
                }
            }

            recursiveShit(startingBone != null ? (Transform)startingBone : ani.GetBoneTransform(HumanBodyBones.Hips), collectDynamicBones(ani.transform));

            //keep bindposes 
            List<Matrix4x4> bindposes = new List<Matrix4x4>();

            for (int b = 0; b < bones.Count; b++)
            {
                bindposes.Add(bones[b].worldToLocalMatrix * ani.transform.worldToLocalMatrix);
            }


            GameObject yourBones = new GameObject(name + "_" + bonename + "_YourBones");
            yourBones.transform.parent = ani.transform;

            SkinnedMeshRenderer yourSkinnedMeshRenderer = yourBones.AddComponent<SkinnedMeshRenderer>();
            yourSkinnedMeshRenderer.sharedMesh = new Mesh
            {
                name = name + "_" + bonename + "_YourBones"
            };
            yourSkinnedMeshRenderer.sharedMesh.CombineMeshes(combineInstances.ToArray());

            Vector3 scale = ani.transform.localScale;
            
            List<Vector3> boneVertices = new List<Vector3>();
            for (int i = 0; i < yourSkinnedMeshRenderer.sharedMesh.vertexCount; i++)
            {
                var vertex = yourSkinnedMeshRenderer.sharedMesh.vertices[i];
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
            yourSkinnedMeshRenderer.sharedMaterials = haveIKLines ? new Material[] { boneMaterial, ikMaterial } : new Material[] { boneMaterial };

            yourSkinnedMeshRenderer.sharedMesh.RecalculateBounds();

            AssetDatabase.CreateAsset(yourSkinnedMeshRenderer.sharedMesh, finalFilePath + "/" + name + "_" + bonename + "_YourBones.asset");
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
        foreach (DynamicBone dynamicBone in transform.GetComponents<DynamicBone>())
        {
            collectDynamicBonesForOneScript(dynamicBone.m_Root, dynamicBone.m_Exclusions, results);
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            collectDynamicBones(transform.GetChild(i), results);
        }
    }

    private void collectDynamicBonesForOneScript(Transform transform, List<Transform> exclusions, HashSet<Transform> results)
    {
        if (exclusions.Contains(transform))
        {
            return;
        }

        results.Add(transform);

        for (int i = 0; i < transform.childCount; i++)
        {
            collectDynamicBonesForOneScript(transform.GetChild(i), exclusions, results);
        }
    }

    private void recursiveShit(Transform transform, HashSet<Transform> dynamicBones)
    {
        bool dynbone = dynamicBones.Contains(transform);
        if (transform.gameObject.activeInHierarchy && bones.Contains(transform))
        {
            for (int i = 0; i < transform.childCount; i++)
            {

                if (transform.GetChild(i).gameObject.activeInHierarchy)
                {
                    addCapColl(transform, transform.GetChild(i), dynbone);
                }

                // Always recurse - can have dynamic bones under non-dynamic bones
                recursiveShit(transform.GetChild(i), dynamicBones);
            }
        }
    }
 
    //tranform1 = main
    //tranform2 = child
    private void addCapColl(Transform transform1, Transform transform2, bool dynbone)
    {    

        GameObject boneSpawn = Instantiate(bone, transform1.position, transform1.rotation) as GameObject;
        float dist =  Vector3.Distance(transform1.position, transform2.position) * 0.5f;
        boneSpawn.name = transform1.name + " -> " + transform2.name;
        boneSpawn.transform.localScale = new Vector3(dist, dist, dist);
        boneSpawn.transform.LookAt(transform2.position);
        boneSpawn.transform.rotation = Quaternion.Euler(boneSpawn.transform.rotation.eulerAngles + new Vector3(90f,0,0));
        bool isHumanoid = false;
        foreach (HumanBodyBones bone in HumanBodyBones.GetValues(typeof(HumanBodyBones)))
        {
            if (ani.GetBoneTransform(bone) != null && ani.GetBoneTransform(bone).name == transform1.name)
            {
                isHumanoid = true;
                break;
            }
        }

        InsertSMRToCombine(boneSpawn.GetComponentInChildren<MeshFilter>(), transform1.name, isHumanoid, dynbone);
        DestroyImmediate(boneSpawn);
    }

    private void InsertSMRToCombine(MeshFilter smr, string bonename, bool hoomanoid, bool dynbone)
    {

        BoneWeight[] meshBoneweight = new BoneWeight[smr.sharedMesh.vertexCount];



        // remap bone weight bone indexes to the hashtable obtained from base object
        foreach (BoneWeight bw in meshBoneweight)
        {
            BoneWeight bWeight = bw;
            
            if (bonesByHash[bonename] != null)
            {
                bWeight.boneIndex0 = (int)bonesByHash[bonename];
                bWeight.weight0 = 1;

                boneWeights.Add(bWeight);
            }
        }

        //add the smr to the combine list; also add to destroy list
        CombineInstance ci = new CombineInstance();
        ci.mesh = smr.sharedMesh;

        ci.transform = smr.transform.localToWorldMatrix;

        Color colour = new Color();
        if (dynbone)
        {
            colour = Color.blue;
        } else if (hoomanoid)
        {
            colour = Color.red;
        }

        for (int i = 0; i < smr.sharedMesh.vertexCount; i++)
        {
            coloUrs.Add(colour);
        }
        combineInstances.Add(ci);
    }

    private void IKLines(List<Vector3> vertices, HumanBodyBones upper, HumanBodyBones joint, HumanBodyBones lower)
    {
        Transform upperT = ani.GetBoneTransform(upper);
        Transform jointT = ani.GetBoneTransform(joint);
        Transform lowerT = ani.GetBoneTransform(lower);

        if (upperT == null) return;
        if (jointT == null) return;
        if (lowerT == null) return;

        BoneWeight[] _boneWeights = new BoneWeight[3];
        _boneWeights[0].boneIndex0 = (int)bonesByHash[upperT.name];
        _boneWeights[0].weight0 = 1;
        _boneWeights[1].boneIndex0 = (int)bonesByHash[lowerT.name];
        _boneWeights[1].weight0 = 1;
        _boneWeights[2].boneIndex0 = (int)bonesByHash[upperT.name];
        _boneWeights[2].weight0 = 1;

        boneWeights.Add(_boneWeights[0]);
        boneWeights.Add(_boneWeights[1]);
        boneWeights.Add(_boneWeights[2]);

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