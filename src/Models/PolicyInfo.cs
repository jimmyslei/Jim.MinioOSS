﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jim.MinioOSS;

public class PolicyInfo
{
    /// <summary>
    /// 
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public List<StatementItem> Statement { get; set; }
}
