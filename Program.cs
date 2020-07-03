using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System;

namespace dsevents
{
    enum Mode
    {
        PAST,
        FUTURE,
    }

    class Program
    {
        static EventsModel model = null;

        static void Main(string[] args)
        {
            string json = "";
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                json += line;
            }

            if (json.Length == 0)
            {
                return;
            }

            model = JsonSerializer.Deserialize<EventsModel>(json);

            string id = args[1];

            Event evt = model.Events.Find(v => v.ID == id);
            Process process = model.Processes.Find(v => v.ID == evt.ProcessID);
            Channel channel = model.Channels.Find(v => v.ID == evt.ChannelID);
            Message message = model.Messages.Find(v => v.ID == evt.MessageID);

            string mode = args[0];

            if (mode == "past")
            {
                List<Event> past = GetEvents(evt, Mode.PAST);
                LogEvents(past);
            }
            else if (mode == "future")
            {
                List<Event> future = GetEvents(evt, Mode.FUTURE);
                LogEvents(future);
            }
            else if (mode == "concurrent")
            {
                List<Event> past = GetEvents(evt, Mode.PAST);
                List<Event> future = GetEvents(evt, Mode.FUTURE);

                List<Event> all = new List<Event>();
                all.AddRange(past);
                all.AddRange(future);

                List<Event> events = model.Events
                    .Where(v => all.Find(x => x.ID == v.ID) == null && v.ID != evt.ID)
                    .OrderBy(v => v.ID)
                    .ToList();
                LogEvents(events);
            }
        }

        private static List<Event> GetEvents(Event evt, Mode mode, int depth = 0)
        {
            Process process = model.Processes.Find(v => v.ID == evt.ProcessID);
            Channel channel = model.Channels.Find(v => v.ID == evt.ChannelID);
            Message message = model.Messages.Find(v => v.ID == evt.MessageID);

            List<Event> events = model.Events
                .Where(
                    v =>
                        v.ProcessID == evt.ProcessID &&
                        (mode == Mode.PAST ? v.Seq < evt.Seq : v.Seq > evt.Seq)
                )
                .ToList();

            if (depth != 0)
            {
                events.Add(evt);
            }

            if (mode == Mode.PAST)
            {
                if (channel == null)
                {
                    return events;
                }

                Event from = model.Events.Find(v =>
                    v.ProcessID == channel.From &&
                    v.ChannelID == evt.ChannelID
                );

                events.AddRange(
                    model.Events.Where(v =>
                        v.ID != evt.ID &&
                        v.ProcessID == channel.From &&
                        v.Seq <= from.Seq
                    )
                );
            }

            events = events
                .GroupBy(v => v.ID)
                .Select(g => g.First())
                .ToList();

            List<Event> temp = new List<Event>();

            List<Event> req = new List<Event>(events);
            req.Add(evt);

            req.ForEach(v =>
            {
                if (v.ChannelID == null)
                {
                    return;
                }

                List<Channel> found = model.Channels
                    .Where(c =>
                            v.ChannelID == c.ID &&
                            (mode == Mode.PAST ? c.From : c.To) != v.ProcessID
                    )
                    .ToList();

                found.ForEach(channel =>
                {
                    List<Event> list = model.Events
                        .Where(x =>
                            x.ID != v.ID &&
                            x.ChannelID == channel.ID &&
                            (x.ProcessID == channel.From || x.ProcessID == channel.To)
                        )
                        .ToList();

                    list.ForEach(x => temp.AddRange(GetEvents(x, mode, depth + 1)));
                });
            });
            events.AddRange(temp);

            return events
                .GroupBy(v => v.ID)
                .Select(g => g.First())
                .ToList();
        }

        private static void LogEvents(List<Event> events)
        {
            Console.WriteLine(
              String.Join(' ', events
                .OrderBy(v => v.ID)
                .Select(v => v.ID)
                .ToList()
              )
            );
        }
    }
}
