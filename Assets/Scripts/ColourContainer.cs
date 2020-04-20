using UnityEngine;

[CreateAssetMenu(fileName = "ColourContainer", menuName = "Create colour container")]
public class ColourContainer : ScriptableObject
{
    [SerializeField] private Color[] m_colours;

    public Color GetColour ( int index )
    {
        return m_colours[index];
    }
}