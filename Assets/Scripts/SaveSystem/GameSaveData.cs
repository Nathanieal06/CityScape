using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityScape.SaveSystem
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Save Slot Metadata  (shown in main-menu / load screen)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lightweight header stored alongside each save slot so the Load screen
    /// can display useful information without deserializing the full save file.
    /// </summary>
    [Serializable]
    public class SaveSlotMetadata
    {
        /// <summary>Slot index (0, 1 or 2).</summary>
        public int    slotIndex;

        /// <summary>ISO-8601 timestamp when this slot was last written.</summary>
        public string timestamp;

        /// <summary>In-game day number at save time.</summary>
        public int    dayCount;

        /// <summary>Player's money at save time.</summary>
        public int    money;

        /// <summary>Player's population at save time.</summary>
        public int    population;

        /// <summary>Human-readable label shown in the UI.</summary>
        public string displayLabel;

        /// <summary>Returns true when this metadata represents a real save (not an empty slot).</summary>
        public bool IsValid => !string.IsNullOrEmpty(timestamp);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Per-Building Save Data
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serialisable snapshot of a single placed building.
    /// Uses string IDs rather than object references so JSON survives asset renames.
    /// </summary>
    [Serializable]
    public class BuildingSaveData
    {
        /// <summary>Matches BuildingData.buildingID for database lookup on load.</summary>
        public string buildingID;

        /// <summary>World-space X position.</summary>
        public float posX;

        /// <summary>World-space Y position.</summary>
        public float posY;

        /// <summary>World-space Z position.</summary>
        public float posZ;

        /// <summary>Quaternion components for rotation.</summary>
        public float rotX, rotY, rotZ, rotW;

        /// <summary>Grid origin X.</summary>
        public int originX;

        /// <summary>Grid origin Y.</summary>
        public int originY;

        /// <summary>Rotation step (0–3, each = 90°).</summary>
        public int rotationStep;

        /// <summary>Upgrade level (future).</summary>
        public int level;

        /// <summary>Whether the building is active/enabled.</summary>
        public bool enabled;

        /// <summary>Constructs a Vector3 world position from stored floats.</summary>
        public Vector3 Position => new Vector3(posX, posY, posZ);

        /// <summary>Constructs a Quaternion from stored floats.</summary>
        public Quaternion Rotation => new Quaternion(rotX, rotY, rotZ, rotW);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Per-Road Save Data
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Serialisable snapshot of a single placed road tile.</summary>
    [Serializable]
    public class RoadSaveData
    {
        /// <summary>Grid X coordinate of the road cell.</summary>
        public int gridX;

        /// <summary>Grid Y coordinate of the road cell.</summary>
        public int gridY;

        /// <summary>Asset name of the road BuildingData used (for variant lookup).</summary>
        public string roadDataName;

        /// <summary>Rotation step applied to this road tile.</summary>
        public int rotationStep;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Full Game Save Data
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Root save-data container written to disk as a single JSON file per slot.
    /// All nested types are plain C# — no Unity object references — making the
    /// file safe to deserialise with JsonUtility across editor and runtime.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        // ── Economy ──────────────────────────────────────────────────────────
        public int   money;
        public int   population;
        public float happiness;
        public float electricityUsed;
        public float electricityCapacity;
        public float waterUsed;
        public float waterCapacity;
        public float wastePercentage;

        // ── Time ─────────────────────────────────────────────────────────────
        /// <summary>Elapsed in-game time in seconds since day 1.</summary>
        public float currentGameTime;
        public int   currentDay;

        // ── Camera ───────────────────────────────────────────────────────────
        public float cameraPosX, cameraPosY, cameraPosZ;
        public float cameraRotX, cameraRotY, cameraRotZ, cameraRotW;
        /// <summary>0 = Build, 1 = Explore.</summary>
        public int   cameraModeIndex;

        // ── Placed Objects ────────────────────────────────────────────────────
        public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
        public List<RoadSaveData>     roads     = new List<RoadSaveData>();

        // ── Progression ───────────────────────────────────────────────────────
        /// <summary>IDs of buildings the player has unlocked.</summary>
        public List<string> unlockedBuildingIDs = new List<string>();

        // ── Settings ──────────────────────────────────────────────────────────
        public float masterVolume    = 1f;
        public float musicVolume     = 0.8f;
        public float sfxVolume       = 1f;
        public int   graphicsQuality = 2;
        public bool  autoSaveEnabled = true;
        public float autoSaveIntervalMinutes = 5f;
    }
}
