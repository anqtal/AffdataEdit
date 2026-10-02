using Dummiesman;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ObjFromStream : MonoBehaviour {
	IEnumerator Start () {
        using (var request = UnityWebRequest.Get("https://people.sc.fsu.edu/~jburkardt/data/obj/lamp.obj"))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Could not download OBJ: {request.error}");
                yield break;
            }
            using (var stream = new MemoryStream(request.downloadHandler.data))
                new OBJLoader().Load(stream);
        }
	}
}
