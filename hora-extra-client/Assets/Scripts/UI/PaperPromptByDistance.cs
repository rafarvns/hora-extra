using UnityEngine;

public class PaperPromptByDistance : MonoBehaviour
{
    [Header("Prompt")]
    [SerializeField] private GameObject promptRoot;

    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Distance")]
    [SerializeField] private float showDistance = 3f;

    private void Start()
    {
        if (promptRoot != null)
            promptRoot.SetActive(false);

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }
    }

    private void Update()
    {
        if (promptRoot == null)
            return;

        if (player == null)
        {
            TryFindPlayer();
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        bool shouldShow = distance <= showDistance;

        if (promptRoot.activeSelf != shouldShow)
            promptRoot.SetActive(shouldShow);
    }

    private void TryFindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }
}