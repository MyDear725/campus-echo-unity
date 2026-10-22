using System;
using System.Collections.Generic;
using UnityEngine;

// One reservable library seat, tied to a real chair in the scene.
//
// The id is built from floor + area + a running index within that area, so it stays the
// same across reloads (no InstanceID, no scene-visit ordering surprises).
[Serializable]
public class LibrarySeat
{
    public string id;          // e.g. 2F-A-012
    public int floor;          // 1, 2 or 3
    public string area;        // A / B / C
    public string areaName;    // 圆厅自习区, 阅览区 ...
    public Vector3 sitPos;     // where the player's root goes
    public Vector3 standPos;   // safe spot to stand up into
    public Quaternion facing;  // seat orientation, facing the table
    public Vector2 mapPos;     // normalised position for the phone map (0..1)

    [NonSerialized] public Transform chair;   // live scene reference, null when unloaded

    public string DisplayId => id;
}
