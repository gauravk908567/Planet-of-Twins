//using Unity.Cinemachine;
//using System.Collections.Generic;
//using UnityEngine;

//public class CameraObstruction : MonoBehaviour
//{
//    public LayerMask obstructionMask;
//    public float sphereRadius = 0.6f;
//    public float fadeValue = 0.3f;

//    CinemachineBrain brain;

//    private HashSet<Renderer> lastHits = new HashSet<Renderer>();
//    private MaterialPropertyBlock block;

//    void Awake()
//    {
//        block = new MaterialPropertyBlock();
//        brain = GetComponent<CinemachineBrain>();
//    }

//    void LateUpdate()
//    {
//        foreach (var r in lastHits)
//        {
//            r.GetPropertyBlock(block);
//            block.SetFloat("_seeThroughDistance", 1f);
//            r.SetPropertyBlock(block);
//        }

//        lastHits.Clear();

//        if (brain.ActiveVirtualCamera == null)
//            return;

//        Transform target = brain.ActiveVirtualCamera.LookAt;

//        if (!target)
//            return;

//        Vector3 dir = target.position - transform.position;
//        float dist = dir.magnitude;

//        RaycastHit[] hits = Physics.SphereCastAll(
//            transform.position,
//            sphereRadius,
//            dir.normalized,
//            dist,
//            obstructionMask
//        );

//        foreach (var hit in hits)
//        {
//            Renderer r = hit.collider.GetComponent<Renderer>();

//            if (r && !lastHits.Contains(r))
//            {
//                r.GetPropertyBlock(block);
//                block.SetFloat("_seeThroughDistance", fadeValue);
//                r.SetPropertyBlock(block);

//                lastHits.Add(r);
//            }
//        }
//    }
//}