using UnityEngine;

// Persistent scene marker for one reservable seat. The builder writes these into the
// library scene; LibrarySeatRegistry collects them at runtime so the phone, the prompt
// and the sit logic all read the same data with no hard-coded coordinates.
public class LibrarySeatPart : MonoBehaviour
{
    public string seatId;
    public int floor;
    public string area;
    public string areaName;

    // Offset from the seat to a spot that was walkable before sitting.
    public Vector3 standOffset = new Vector3(0f, 0f, -0.75f);

    public LibrarySeat ToSeat()
    {
        return new LibrarySeat
        {
            id = seatId,
            floor = floor,
            area = area,
            areaName = areaName,
            sitPos = new Vector3(transform.position.x, transform.position.y, transform.position.z),
            standPos = transform.position + standOffset,
            facing = transform.rotation,
            chair = transform
        };
    }
}
