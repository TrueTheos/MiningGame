using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Assets.Scripts
{
    [BurstCompile]
    public struct CellularAutomataJob : IJobParallelFor
    {
        [ReadOnly] public int Width;
        [ReadOnly] public int Height;
        [ReadOnly] public int DeathLimit;
        [ReadOnly] public NativeArray<int> BirthLimits;
        [ReadOnly] public NativeArray<bool> CurrentMap;

        [WriteOnly] public NativeArray<bool> NewMap;

        public void Execute(int index)
        {
            int x = index % Width;
            int y = index / Width;

            int neighborCount = CountNeighbors(x, y);
            bool currentCell = CurrentMap[index];

            if (currentCell)
            {
                NewMap[index] = neighborCount >= DeathLimit;
            }
            else
            {
                // Pick a random birth limit from the array using a deterministic method
                int birthLimitIndex = (x * 48271 + y * 40961) % BirthLimits.Length;
                NewMap[index] = neighborCount >= BirthLimits[birthLimitIndex];
            }
        }

        private int CountNeighbors(int x, int y)
        {
            int count = 0;
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;

                    int nx = x + i;
                    int ny = y + j;

                    // Count out-of-bounds as solid
                    if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                    {
                        count++;
                        continue;
                    }

                    if (CurrentMap[ny * Width + nx])
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
