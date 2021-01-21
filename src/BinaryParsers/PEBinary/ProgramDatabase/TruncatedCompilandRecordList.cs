// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    /// <summary>A list of <see cref="CompilandRecord"/> truncated to a reasonable number of records for display purposes.</summary>
    public class TruncatedCompilandRecordList : IEnumerable<CompilandRecord>
    {
        private const int DEFAULT_MAX_RECORDS = 100;
        private readonly List<CompilandRecord> rawRecords;
        private readonly int maxRecords;
        private bool sorted;

        /// <summary>Initializes a new instance of the <see cref="TruncatedCompilandRecordList"/> class.</summary>
        public TruncatedCompilandRecordList()
            : this(DEFAULT_MAX_RECORDS)
        { }

        /// <summary>Initializes a new instance of the <see cref="TruncatedCompilandRecordList"/> class
        /// with the indicated library object file count limit.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="_maxRecords"/> is negative.</exception>
        /// <param name="maxRecords">The maximum number of printed records.</param>
        public TruncatedCompilandRecordList(int maxRecords)
        {
            // 4 comes from:
            // 
            // 1. At least 1 object file
            // 2. Truncation message for library containing said object file
            // 3. Truncation message for all libraries completely truncated
            // 4. Truncation message totalling all truncated stuff
            if (maxRecords < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRecords), maxRecords, "Max record count must be at least 4 to leave room for truncation messages.");
            }

            this.rawRecords = new List<CompilandRecord>();
            this.maxRecords = maxRecords;
        }

        /// <summary>Adds record to this list.</summary>
        /// <param name="record">The record to add.</param>
        public void Add(CompilandRecord record)
        {
            this.sorted = false;
            this.rawRecords.Add(record);
        }

        /// <summary>Gets a value indicating whether or not this list is empty.</summary>
        /// <value>true if empty, false if not.</value>
        public bool Empty => this.rawRecords.Count == 0;

        /// <summary>Creates a sorted formatted object list string.</summary>
        /// <returns>The sorted, formatted object list.</returns>
        public string CreateSortedObjectList()
        {
            this.EnsureSorted();

            return this.CreateAllObjectList();
        }

        public override string ToString()
        {
            return this.CreateSortedObjectList();
        }

        /// <summary>Creates truncated object list for display purposes.</summary>
        /// <returns>The new truncated object list.</returns>
        public string CreateTruncatedObjectList()
        {
            this.EnsureSorted();
            if (this.rawRecords.Count <= this.maxRecords)
            {
                // No truncation necessary.
                // (Avoids unnecessary calulation and prevents infinite loops;
                // since remainingSize must eventually reach 0)
                return this.CreateAllObjectList();
            }

            var blockStarts = new List<int>();
            var blockDisplaySizes = new List<int>();
            // Size of [ blockStarts[n], blockStarts[n + 1] ) is in blockSizes[n]

            // Build `blockStarts` and `blockDisplaySizes` by trying to give each library
            // `targetLibraryBlockSize` space
            int remainingSpace = this.TryRoughFitOfBlocksOfTargetLibraryBlockSize(blockStarts, blockDisplaySizes, this.maxRecords - 1 /* -1 for total truncated records message */);
            bool anyLibrariesAreCompletelyTruncated = blockStarts[blockStarts.Count - 1] != this.rawRecords.Count;
            if (anyLibrariesAreCompletelyTruncated)
            {
                // There will be a "completely removed libraries" block; recalculate with 1 less remainingSpace
                // to allow for that message
                remainingSpace = this.TryRoughFitOfBlocksOfTargetLibraryBlockSize(blockStarts, blockDisplaySizes, this.maxRecords - 2);
            }

            ExpandDisplayedBlockSizesToConsumeRemainingSpace(blockStarts, blockDisplaySizes, remainingSpace);

            var sb = new StringBuilder();
            int totalTruncatedLibrariesCount = 0;
            int totalTruncatedObjectsCount = 0;
            for (int idx = 0; idx < blockDisplaySizes.Count; ++idx)
            {
                int blockStart = blockStarts[idx];
                int blockSize = blockStarts[idx + 1] - blockStart;
                int blockSizeDisplay = blockDisplaySizes[idx];
                if (blockSizeDisplay == blockSize)
                {
                    // No truncation
                    this.AppendRecords(sb, blockStart, blockStart + blockSizeDisplay);
                }
                else
                {
                    // Truncation
                    int truncatedRecords = blockSize - blockSizeDisplay + 1; // +1 for truncation message

                    this.AppendRecords(sb, blockStart, blockStart + blockSize - truncatedRecords);

                    // (X object files truncated from Y)
                    sb.Append('(');
                    sb.Append(truncatedRecords.ToString(CultureInfo.InvariantCulture));
                    sb.Append(" object files truncated");
                    string library = this.rawRecords[blockStart].Library;
                    if (library != null)
                    {
                        sb.Append(" from ");
                        sb.Append(library);
                    }
                    sb.AppendLine(")");
                    totalTruncatedObjectsCount += truncatedRecords;
                    ++totalTruncatedLibrariesCount;
                }
            }

            if (anyLibrariesAreCompletelyTruncated)
            {
                int ptr = blockStarts[blockStarts.Count - 1];
                int totallyTruncatedObjects = this.rawRecords.Count - ptr;
                int totallyTruncatedLibraries = 0;
                while (ptr != this.rawRecords.Count)
                {
                    ++totallyTruncatedLibraries;
                    ptr = this.FindNextLibraryTransition(ptr);
                }

                // (X entire libraries truncated containing Y object files)
                sb.Append('(');
                sb.Append(totallyTruncatedLibraries.ToString(CultureInfo.InvariantCulture));
                sb.Append(" entire libraries truncated containing ");
                sb.Append(totallyTruncatedObjects.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine(" object files)");

                totalTruncatedObjectsCount += totallyTruncatedObjects;
                totalTruncatedLibrariesCount += totallyTruncatedLibraries;
            }

            if (totalTruncatedObjectsCount != 0)
            {
                // (X total objects truncated from Y total libraries, use pdbinfo.exe to list all objects)
                sb.Append('(');
                sb.Append(totalTruncatedObjectsCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(" total objects truncated from ");
                sb.Append(totalTruncatedLibrariesCount.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine(" total libraries, use pdbinfo.exe to list all objects)");
            }

            string result = sb.ToString();
            Debug.Assert(result.Count(ch => ch == Environment.NewLine[0]) == this.maxRecords);
            return result;
        }

        /// <summary>Gets the enumerator.</summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<CompilandRecord> GetEnumerator()
        {
            return ((IEnumerable<CompilandRecord>)this.rawRecords).GetEnumerator();
        }

        /// <summary>Gets the enumerator.</summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<CompilandRecord>)this.rawRecords).GetEnumerator();
        }

        private int TryRoughFitOfBlocksOfTargetLibraryBlockSize(List<int> blockStarts, List<int> blockDisplaySizes, int spaceToFitFor)
        {
            blockStarts.Clear();
            blockDisplaySizes.Clear();
            blockStarts.Add(0);

            // We try to have roughly an equal number of libraries as objects in those libraries
            // so as not to bias towards huge libraries or huge numbers of libraries
            int targetLibraryBlockSize = Math.Max(2, (int)Math.Truncate(Math.Sqrt(spaceToFitFor)));

            int next = 0;
            for (int ptr = 0; spaceToFitFor != 0 && ptr != this.rawRecords.Count; ptr = next)
            {
                next = this.FindNextLibraryTransition(ptr);
                blockStarts.Add(next);
                int consumed = Math.Min(Math.Min(targetLibraryBlockSize, next - ptr), spaceToFitFor);
                spaceToFitFor -= consumed;
                blockDisplaySizes.Add(consumed);
            }

            if (blockDisplaySizes.Count == blockStarts.Count)
            {
                // Last "block start" has no "n + 1" record and represents
                // the entire set of truncated entire libraries
                blockStarts.Add(this.FindNextLibraryTransition(blockStarts[blockStarts.Count - 1]));
            }

            return spaceToFitFor;
        }

        private static void ExpandDisplayedBlockSizesToConsumeRemainingSpace(List<int> blockStarts, List<int> blockDisplaySizes, int remainingSpace)
        {
            Debug.Assert(blockDisplaySizes.Count != 0);
            Debug.Assert(blockDisplaySizes.Count == blockStarts.Count - 1);
            int idx = 0;
            while (remainingSpace != 0) // We know this terminates because maxRecords > rawRecords.Count
            {
                int blockStart = blockStarts[idx];
                int blockSize = blockStarts[idx + 1] - blockStart;
                int blockDisplaySize = blockDisplaySizes[idx];
                // See if this block can be bigger
                if (blockDisplaySize != blockSize)
                {
                    ++blockDisplaySizes[idx];
                    --remainingSpace;
                }

                // Rotate idx
                ++idx;
                if (idx == blockDisplaySizes.Count)
                {
                    idx = 0;
                }
            }
        }

        private int FindNextLibraryTransition(int startAt)
        {
            this.AssertIndex(startAt);

            if (startAt == this.rawRecords.Count)
            {
                return startAt;
            }

            string firstLib = this.rawRecords[startAt].Library;
            while (startAt < this.rawRecords.Count &&
                StringComparer.OrdinalIgnoreCase.Equals(firstLib, this.rawRecords[startAt].Library))
            {
                ++startAt;
            }

            return startAt;
        }

        [Conditional("DEBUG")]
        private void AssertRange(int first, int last)
        {
            Debug.Assert(first <= last);
            this.AssertIndex(first);
            this.AssertIndex(last);
        }

        [Conditional("DEBUG")]
        private void AssertIndex(int index)
        {
            Debug.Assert(index >= 0 && index <= this.rawRecords.Count, "Record index out of range");
        }

        private void AppendRecords(StringBuilder sb, int first, int last)
        {
            this.AssertRange(first, last);

            for (; first != last; ++first)
            {
                this.rawRecords[first].AppendString(sb);
                sb.AppendLine();
            }
        }

        private string CreateAllObjectList()
        {
            var sb = new StringBuilder();
            foreach (CompilandRecord record in this.rawRecords)
            {
                record.AppendString(sb);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void EnsureSorted()
        {
            if (this.sorted)
            {
                return;
            }

            this.rawRecords.Sort();
            this.sorted = true;
        }
    }
}
