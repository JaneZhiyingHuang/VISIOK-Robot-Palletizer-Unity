using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; 
public class ConsoleTrigger : MonoBehaviour
{
    [Header("UI Objecct")]
    public GameObject uiCanvas;   

    [Header("UI Scale")]
    public Vector3 bigScale = new Vector3(0.001f, 0.001f, 0.001f);
    public Vector3 smallScale = new Vector3(0.00045f, 0.00045f, 0.00045f);

    [Header("BigScale position")]
    public Vector3 openPosition = new Vector3(-0.08f, 1.92f, 3.732f);

    private Vector3 closedPosition;

    private bool isExpanded = false; 

    void Start()
    {
        if (uiCanvas != null)
        {
            closedPosition = uiCanvas.transform.position;

            uiCanvas.transform.localScale = smallScale;
            uiCanvas.transform.position = closedPosition;

            uiCanvas.SetActive(true);
        }
    }

    public void ToggleMode()
    {
        if (uiCanvas != null)
        {
            if (isExpanded)
            {

                uiCanvas.transform.localScale = smallScale;
                uiCanvas.transform.position = closedPosition;

                isExpanded = false;
            }
            else
            {

                uiCanvas.transform.localScale = bigScale;
                uiCanvas.transform.position = openPosition;

                isExpanded = true;
            }
        }
    }
}