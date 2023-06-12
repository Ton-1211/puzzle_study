using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationController
{
    const float DELTA_TIME_MAX = 1.0f;
    int _time = 0;
    float _inv_time_max = 1.0f;

    public void Set(int max_time)
    {
        Debug.Assert(0.0f < max_time);//•‰‚Ì‘JˆÚŽžŠÔ‚Í•s³

        _time = max_time;
        _inv_time_max = 1.0f / (float)max_time;
    }
    
    public bool Update()
    {
        _time = Mathf.Max(--_time, 0);
        return (0 < _time);
    }

    public float GetNormalized()
    {
        return _inv_time_max * (float)_time;
    }
}
