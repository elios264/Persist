using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PersistDotNet.Persist;

namespace Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            XmlSerializer classroomSerializer = new XmlSerializer(typeof(Classroom));

            using (var writeStream = new FileStream("classroom.xml", FileMode.Create))
                classroomSerializer.Write(writeStream, "Classroom", Classroom.SampleClassroom());

            using (var readStream = new FileStream("classroom.xml", FileMode.Open))
            {
                Classroom newClassroom = (Classroom)classroomSerializer.Read(readStream, "Classroom");
            }

            XmlSerializer automataSerializer = new XmlSerializer(typeof (Automata), new[] {typeof (CommandTransition), typeof (ConditionTransition)});


            using (var writeStream = new FileStream("automata.xml", FileMode.Create))
                automataSerializer.Write(writeStream, "automata", Automata.SampleAutomata());

            using (var readStream = new FileStream("automata.xml", FileMode.Open))
            {
                Automata newAutomata = (Automata)automataSerializer.Read(readStream, "automata");
            }

        }
    }
}
