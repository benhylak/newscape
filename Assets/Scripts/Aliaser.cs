using System.Collections;
using System.Collections.Generic;
using System;

//TODO
//multiple terms available, choose random
//don't interfere with IDF
class Aliaser : Dictionary<string, string>
{
    public Aliaser(params Alias[] aliases) : base(StringComparer.OrdinalIgnoreCase)
    {
        foreach(var alias in aliases)
        {
            foreach(string input in alias.inputs)
            {
                this[input] = alias.output;
            }
        }
    }

    public class Alias
    {
        public string[] inputs;
        public string output;

        public Alias(string output, params string[] inputs)
        {  
            this.output = output;
            this.inputs = inputs;
        }
    }

    public static Aliaser makeDefaultAliaser()
    {
        return new Aliaser(
            new Alias("baseball",

                "homerun",
                "home run",
                "ballgame",
                "ball game",
                "yankees",
                "red sox",
                "phillies",
                "MLB"),
            
            new Alias("football",

                "field goal",
                "touchdown",
                "yard penality",
                "NFL"),

            new Alias("LGBT",

                "gay",
                "trans",
                "transexual",
                "lesbian",
                "bisexual",
                "pride",
                "gay pride"),

            new Alias("army",

                "military",
                "troops",
                "invasion",
                "tanks",
                "war")
        );
    }
}
