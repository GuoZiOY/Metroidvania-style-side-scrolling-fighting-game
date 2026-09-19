using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICounterable
{
    bool IsInCounterTime { get; }
    bool CanBeChased { get; }
    void HandleCounter(float knockbackMultiplier = 1f);
}
