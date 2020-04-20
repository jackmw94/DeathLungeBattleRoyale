using UnityEngine;

[ExecuteAlways]
public class CameraController : MonoBehaviour
{
    [SerializeField] private float m_zoomHeightFactor = 8f;
    [SerializeField] private float m_zoomDistanceFactor = 4f;
    [SerializeField] private Vector2 m_offset;
    private Transform m_transform;

    private static Transform s_followPlayer;

    private void Start()
    {
        m_transform = transform;

        UpdateTransform( Vector3.zero );
    }

    private void Update()
    {
        if ( !s_followPlayer )
        {
            return;
        }

        float adjustHeight = 0f;
        float adjustDistance = 0f;
        if ( Input.GetKey( KeyCode.W ) )
        {
            adjustHeight += Time.deltaTime * m_zoomHeightFactor;
            adjustDistance += Time.deltaTime * m_zoomDistanceFactor;
        }

        if ( Input.GetKey( KeyCode.S ) )
        {
            adjustHeight -= Time.deltaTime * m_zoomHeightFactor;
            adjustDistance -= Time.deltaTime * m_zoomDistanceFactor;
        }

        m_offset.x += adjustDistance;
        m_offset.y += adjustHeight;

        UpdateTransform( s_followPlayer.position );
    }

    private void UpdateTransform( Vector3 currentPosition )
    {
        m_transform.position = currentPosition + new Vector3( 0f, m_offset.y, -m_offset.x );
        m_transform.rotation = Quaternion.Euler( 90f - Mathf.Rad2Deg * Mathf.Atan( m_offset.x / m_offset.y ), 0f, 0f );
    }

    public static void SetFollowTarget( Transform newTarget )
    {
        s_followPlayer = newTarget;
    }
}
