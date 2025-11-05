// There was a dream, but life has taught me dreams don't come true.
// using UnityEngine;
//
// public class TimeController : MonoBehaviour
// {
//     static TimeController _instance;
//     public static TimeController Instance
//     {
//         get
//         {
//             if (_instance == null)
//             {
//                 _instance = FindObjectOfType<TimeController>();
//                 if (_instance == null)
//                 {
//                     var go = new GameObject("TimeController");
//                     _instance = go.AddComponent<TimeController>();
//                     DontDestroyOnLoad(go);
//                 }
//             }
//             return _instance;
//         }
//     }
//
//     public float TimeScale = 1f;
// }