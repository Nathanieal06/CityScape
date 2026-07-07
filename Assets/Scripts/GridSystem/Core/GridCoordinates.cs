using System;
using UnityEngine;

namespace CityScape.GridSystem.Core
{
    /// <summary>
    /// Represents an integer coordinate pair (X, Y) in grid space.
    /// Using a struct keeps this allocation-free and stack-resident.
    /// Implements IEquatable for use as a Dictionary key without boxing.
    /// </summary>
    [Serializable]
    public struct GridCoordinates : IEquatable<GridCoordinates>
    {
        // ─────────────────────────────────────────────
        //  Fields
        // ─────────────────────────────────────────────

        /// <summary>Column index on the grid (left → right).</summary>
        public int X;

        /// <summary>Row index on the grid (bottom → top, i.e. along world Z).</summary>
        public int Y;

        // ─────────────────────────────────────────────
        //  Constructors
        // ─────────────────────────────────────────────

        public GridCoordinates(int x, int y)
        {
            X = x;
            Y = y;
        }

        // ─────────────────────────────────────────────
        //  Arithmetic Operators
        // ─────────────────────────────────────────────

        public static GridCoordinates operator +(GridCoordinates a, GridCoordinates b)
            => new GridCoordinates(a.X + b.X, a.Y + b.Y);

        public static GridCoordinates operator -(GridCoordinates a, GridCoordinates b)
            => new GridCoordinates(a.X - b.X, a.Y - b.Y);

        public static GridCoordinates operator *(GridCoordinates a, int scalar)
            => new GridCoordinates(a.X * scalar, a.Y * scalar);

        // ─────────────────────────────────────────────
        //  Equality
        // ─────────────────────────────────────────────

        public bool Equals(GridCoordinates other)
            => X == other.X && Y == other.Y;

        public override bool Equals(object obj)
            => obj is GridCoordinates other && Equals(other);

        public override int GetHashCode()
        {
            // Use a prime-based hash to minimise collisions in Dictionary.
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X;
                hash = hash * 31 + Y;
                return hash;
            }
        }

        public static bool operator ==(GridCoordinates a, GridCoordinates b) => a.Equals(b);
        public static bool operator !=(GridCoordinates a, GridCoordinates b) => !a.Equals(b);

        // ─────────────────────────────────────────────
        //  Conversion Helpers
        // ─────────────────────────────────────────────

        /// <summary>Converts to Unity's Vector2Int for interop with other Unity APIs.</summary>
        public Vector2Int ToVector2Int() => new Vector2Int(X, Y);

        /// <summary>Creates a GridCoordinates from a Vector2Int.</summary>
        public static GridCoordinates FromVector2Int(Vector2Int v) => new GridCoordinates(v.x, v.y);

        // ─────────────────────────────────────────────
        //  Debug
        // ─────────────────────────────────────────────

        public override string ToString() => $"({X}, {Y})";

        // ─────────────────────────────────────────────
        //  Static Shorthands
        // ─────────────────────────────────────────────

        public static readonly GridCoordinates Zero  = new GridCoordinates(0, 0);
        public static readonly GridCoordinates One   = new GridCoordinates(1, 1);
        public static readonly GridCoordinates Right = new GridCoordinates(1, 0);
        public static readonly GridCoordinates Up    = new GridCoordinates(0, 1);
    }
}
