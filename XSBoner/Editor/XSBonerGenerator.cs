using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;

public class XSBonerGenerator : EditorWindow {
    private Animator ani;
    private static GameObject boneModel;
    private SkinnedMeshRenderer smr;
    private static Material boneMaterial;
    private static Material ikMaterial;
    private bool haveIKLines;
    private bool spookMode;
    private List<Transform> bones;
    private Hashtable bonesByHash;
    private List<BoneWeight> boneWeights;
    private List<CombineInstance> combineInstances;
    private List<Color> coloUrs;
    private Object startingBone;
    private int vertCount = 0;
    private string finalFilePath = null;
    private string pathToGenerated;
    private string editorPath;
    private int totalAddedForCounting;

    [MenuItem("Xiexe/Tools/XSBonerGenerator")]
    static void Init()
    {
        XSBonerGenerator window = (XSBonerGenerator)GetWindow(typeof(XSBonerGenerator));
        window.Show();
    }

    private void OnGUI()
    {
        if (finalFilePath == null) {
            finalFilePath = findAssetPath();
            pathToGenerated = finalFilePath + "/Generated";
            editorPath = finalFilePath + "/Editor";

            if (!Directory.Exists(pathToGenerated)) {
                Directory.CreateDirectory(pathToGenerated);
            }
            
            // Defaults
            boneModel = (GameObject)AssetDatabase.LoadAssetAtPath(finalFilePath + "/Bone Stuff/Bone Models/Unity Mecanim Bone.obj", typeof(GameObject));
            boneMaterial = (Material)AssetDatabase.LoadAssetAtPath(finalFilePath + "/Bone Stuff/Materials/Bones/Mecanim Colors.mat", typeof(Material));
            ikMaterial = (Material)AssetDatabase.LoadAssetAtPath(finalFilePath + "/Bone Stuff/Materials/IK Lines/IKLine Yellow.mat", typeof(Material));
        }

        bool aniChanged = false;
        Animator ani_old = ani;
        ani = (Animator)EditorGUILayout.ObjectField(new GUIContent("Animator Object", "Your Model's Animator object"), ani, typeof(Animator), true);
        if (ani != ani_old) aniChanged = true;

        if (ani == null) {
            startingBone = null;
        }

        if (ani && !ani.isHuman)
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
        
        bool recountVerts = false;
        GameObject bone_old = boneModel;
        boneModel = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Bone Model", "The Model to use as the bone"), boneModel, typeof(GameObject), true);
        if (boneModel != bone_old) recountVerts = true;

        SkinnedMeshRenderer smr_old = smr;
        // Find first SkinnedMeshRenderer that's under the main avatar transform
        if (ani != null && aniChanged)
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
        smr = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(new GUIContent("Skinned Mesh Renderer", "The main skinned mesh renderer"), smr, typeof(SkinnedMeshRenderer), true);
        if (smr != smr_old) recountVerts = true;

        boneMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("Bone Material", "The Material you want for your bones"), boneMaterial, typeof(Material), true);

        // Counts the total amount of vertices for the bone object multiplied by armature bones
        if (recountVerts && boneModel != null) {
            GameObject boneMesh = Instantiate(boneModel) as GameObject;
            if (boneMesh.GetComponentInChildren<MeshFilter>()) {
                vertCount = boneMesh.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount;
            } else {
                vertCount = -1;
            }
            DestroyImmediate(boneMesh);

            List<Transform> _bones = new List<Transform>();
            foreach (Transform _bone in smr.bones)
            {
                if (_bone != null) {
                    _bones.Add(_bone);
                }
            }
            totalAddedForCounting = 0;
            recursiveShitForCounting(startingBone != null ? (Transform)startingBone : ani.GetBoneTransform(HumanBodyBones.Hips), _bones);
            vertCount = vertCount*totalAddedForCounting+12; // 12 for the IK verts even though they might not be enabled, it wont matter that much
        }

        if (ani && ani.isHuman) {
            if (haveIKLines) {
                ikMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("IK Material", "The Material you want for your IK Lines"), ikMaterial, typeof(Material), true);
            }
            haveIKLines = EditorGUILayout.Toggle("Have IK Lines", haveIKLines);
        } else
        {
            haveIKLines = false;
        }

        //Toggle for Spook Mode
        spookMode = EditorGUILayout.Toggle("Spook Mode (Optional)", spookMode);

        EditorGUILayout.LabelField("Vert Count", System.String.Format("{0:#,##0}", vertCount));

        bool error = false;
        if (ani == null)
        {
            EditorGUILayout.HelpBox("No Animator found", MessageType.Error);
            error = true;
        }
        if (boneModel == null)
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
        if (vertCount > 65000) {
            EditorGUILayout.HelpBox("The total amount of verts that this would create is too high", MessageType.Error);
            error = true;
        }

        if (error) return;
        
        EditorGUILayout.Separator();

        if (GUILayout.Button("Generate"))
        {
            // Clears up non alphanumeric characters because Windows
            string name = Regex.Replace(ani.name, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);
            string bonename = Regex.Replace(boneModel.name, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);

            bones = new List<Transform>();
            bonesByHash = new Hashtable();
            boneWeights = new List<BoneWeight>();
            combineInstances = new List<CombineInstance>();
            coloUrs = new List<Color>();

            // Grabs all bones and their index
            // This might actually be wrong but it seemed to work
            int boneIndex = 0;
            foreach (Transform _bone in smr.bones)
            {
                if (_bone != null) {
                    bones.Add(_bone);
                    bonesByHash.Add(_bone.name, boneIndex);
                    boneIndex++;
                }
            }

            // Start the entire motion
            recursiveShit(startingBone != null ? (Transform)startingBone : ani.GetBoneTransform(HumanBodyBones.Hips), collectDynamicBones(ani.transform));

            // Keep bindposes 
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

           //Adding Audio Source for Super Spooky Mode.
            if (spookMode){
                yourBones.AddComponent<AudioSource>();
                AudioSource doot = yourBones.GetComponent<AudioSource>();
                doot.clip = (AudioClip)AssetDatabase.LoadAssetAtPath(editorPath + "/Doot.mp3", typeof(AudioClip));
                doot.spatialBlend = 1;
                doot.dopplerLevel = 0;
                doot.minDistance = 2;
                doot.maxDistance = 10;
            }

            // Combines all the bone model meshes
            yourSkinnedMeshRenderer.sharedMesh.CombineMeshes(combineInstances.ToArray());

            // Scales and moves the bone model verts to the correct size and location
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

            // Generates the IK Lines vertices
            if (haveIKLines)
            {
                IKLines(boneVertices, HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm);
                IKLines(boneVertices, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm);
                IKLines(boneVertices, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg);
                IKLines(boneVertices, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg);
            }

            yourSkinnedMeshRenderer.sharedMesh.vertices = boneVertices.ToArray();

            // Sets a new submesh for the IK Lines
            if (haveIKLines)
            {
                yourSkinnedMeshRenderer.sharedMesh.subMeshCount = 2;
                int[] values = Enumerable.Range(yourSkinnedMeshRenderer.sharedMesh.vertexCount - 12, 12).ToArray(); // Magic value 12 because that's how many verts are in 4 IK Lines
                yourSkinnedMeshRenderer.sharedMesh.SetTriangles(values, 1);
            }

            yourSkinnedMeshRenderer.bones = bones.ToArray();
            yourSkinnedMeshRenderer.rootBone = bones[0];
            yourSkinnedMeshRenderer.sharedMesh.boneWeights = boneWeights.ToArray();
            yourSkinnedMeshRenderer.sharedMesh.bindposes = bindposes.ToArray();
            yourSkinnedMeshRenderer.sharedMesh.colors = coloUrs.ToArray();
            yourSkinnedMeshRenderer.sharedMaterials = haveIKLines ? new Material[] { boneMaterial, ikMaterial } : new Material[] { boneMaterial };

            yourSkinnedMeshRenderer.sharedMesh.RecalculateBounds();

            AssetDatabase.CreateAsset(yourSkinnedMeshRenderer.sharedMesh, pathToGenerated + "/" + name + "_" + bonename + "_YourBones.asset");
            AssetDatabase.SaveAssets();
            ani = null;
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
            Transform root = dynamicBone.m_Root;
            List<Transform> exclusions = dynamicBone.m_Exclusions;

            // Faithfully replicating Dynamic Bone quirk where excluding the root (AKA target bone) is ignored.
            exclusions.Remove(root);

            collectDynamicBonesForOneScript(root, exclusions, results);
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
                    createBone(transform, transform.GetChild(i), dynbone);
                }

                // Always recurs - can have dynamic bones under non-dynamic bones
                recursiveShit(transform.GetChild(i), dynamicBones);
            }
        }
    }

    private void recursiveShitForCounting(Transform transform, List<Transform> _bones)
    {
        if (transform.gameObject.activeInHierarchy && _bones.Contains(transform))
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).gameObject.activeInHierarchy)
                {
                    totalAddedForCounting += 1;
                }
                recursiveShitForCounting(transform.GetChild(i), _bones);
            }
        }
    }

    // Creates a bone mesh to be combined
    private void createBone(Transform parentTransform, Transform childTransform, bool dynbone)
    {
        GameObject boneSpawn = Instantiate(boneModel, parentTransform.position, parentTransform.rotation) as GameObject;
        float dist =  Vector3.Distance(parentTransform.position, childTransform.position) * 0.5f;
        boneSpawn.name = parentTransform.name + " -> " + childTransform.name;
        boneSpawn.transform.localScale = new Vector3(dist, dist, dist);
        boneSpawn.transform.LookAt(childTransform.position);
        boneSpawn.transform.rotation = Quaternion.Euler(boneSpawn.transform.rotation.eulerAngles + new Vector3(90f,0,0));
        bool isHumanoid = false;
        foreach (HumanBodyBones bone in HumanBodyBones.GetValues(typeof(HumanBodyBones)))
        {
            if (ani.GetBoneTransform(bone) != null && ani.GetBoneTransform(bone).name == parentTransform.name)
            {
                isHumanoid = true;
                break;
            }
        }

        InsertSMRToCombine(boneSpawn.GetComponentInChildren<MeshFilter>(), parentTransform.name, isHumanoid, dynbone);
        DestroyImmediate(boneSpawn);
    }

    private void InsertSMRToCombine(MeshFilter smr, string bonename, bool hoomanoid, bool dynbone)
    {

        BoneWeight[] meshBoneweight = new BoneWeight[smr.sharedMesh.vertexCount];

        // Remap bone weight bone indexes to the hashtable obtained from base object
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

        // Add the smr to the combine list
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

    // Add vertices to the vertices list for the IK Lines
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

    private static string findAssetPath() {
        string[] guids1 = AssetDatabase.FindAssets("XSBonerGenerator", null); // Get unique file location
        string untouchedString = AssetDatabase.GUIDToAssetPath(guids1[0]);
        string[] splitString = untouchedString.Split('/');

        ArrayUtility.RemoveAt(ref splitString, splitString.Length - 1);
        ArrayUtility.RemoveAt(ref splitString, splitString.Length - 1);
        
        string finalFilePath = string.Join("/", splitString);

        return finalFilePath;
    }
}