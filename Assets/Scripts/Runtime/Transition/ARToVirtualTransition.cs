using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ARToVirtualTransition : MonoBehaviour
{
    [Header("Nama Scene")]
    [SerializeField] private string virtualSceneName = "VirtualRoom";
    [SerializeField] private string arSceneName = "SampleScene";

    private bool isLoading;

    public void EnterVirtualRoom()
    {
        LoadScene(virtualSceneName);
    }

    public void ReturnToAR()
    {
        LoadScene(arSceneName);
    }

    private void LoadScene(string sceneName)
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("ARToVirtualTransition: Nama scene belum diisi.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                "ARToVirtualTransition: Scene '" + sceneName +
                "' belum tersedia di Build Settings.",
                this
            );
            return;
        }

        isLoading = true;
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        if (operation == null)
        {
            isLoading = false;
            Debug.LogError(
                "ARToVirtualTransition: Gagal memulai pemuatan scene '" +
                sceneName + "'.",
                this
            );
            yield break;
        }

        while (!operation.isDone)
            yield return null;
    }
}
