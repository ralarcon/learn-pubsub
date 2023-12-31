﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


internal class Message
{
    public string ClientId { get; set; } = default!;
    public DateTime SourceTimestamp { get; set; }
    public string Content { get; set; } = default!;
    public int SequenceId { get; set; }
    public Guid BatchId { get; set; }
    public DateTime? E4KSystemTimestamp { get; set; } = null;
}
