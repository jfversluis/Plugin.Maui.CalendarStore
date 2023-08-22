using System;
using System.Collections.Generic;

namespace Plugin.Maui.Calendar;

public class Calendar
{
    public Calendar()
    {
    }

    public Calendar(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; set; }

    public string Name { get; set; }
}