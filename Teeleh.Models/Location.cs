﻿namespace Teeleh.Models
{
    public class Location
    {
        public int Id { get; set; }
        public virtual Location Parent { get; set; }
        public int? ParentId { get; set; }
        public LocationType Type { get; set; }
        public string  Name { get; set; }

        public enum LocationType
        {
            PROVINCE,
            CITY,
            REGION
        }
   }
}
