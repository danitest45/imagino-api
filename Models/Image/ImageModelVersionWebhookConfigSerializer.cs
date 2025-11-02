using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Imagino.Api.Models.Image
{
    public sealed class ImageModelVersionWebhookConfigSerializer : SerializerBase<ImageModelVersionWebhookConfig?>
    {
        public override ImageModelVersionWebhookConfig? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var reader = context.Reader;
            var currentType = reader.GetCurrentBsonType();

            return currentType switch
            {
                BsonType.Null => ReadNull(reader),
                BsonType.String => new ImageModelVersionWebhookConfig
                {
                    Url = reader.ReadString()
                },
                BsonType.Document => DeserializeDocument(reader),
                _ => throw new FormatException($"Cannot deserialize WebhookConfig from BsonType.{currentType}.")
            };
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, ImageModelVersionWebhookConfig? value)
        {
            var writer = context.Writer;
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartDocument();

            if (!string.IsNullOrWhiteSpace(value.Url))
            {
                writer.WriteName("url");
                writer.WriteString(value.Url);
            }

            if (value.Events is { Count: > 0 })
            {
                writer.WriteName("events");
                writer.WriteStartArray();
                foreach (var evt in value.Events)
                {
                    writer.WriteString(evt);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndDocument();
        }

        private static ImageModelVersionWebhookConfig? ReadNull(IBsonReader reader)
        {
            reader.ReadNull();
            return null;
        }

        private static ImageModelVersionWebhookConfig DeserializeDocument(IBsonReader reader)
        {
            string? url = null;
            List<string>? events = null;

            reader.ReadStartDocument();

            while (reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var name = reader.ReadName();
                switch (name)
                {
                    case "url":
                        if (reader.GetCurrentBsonType() == BsonType.Null)
                        {
                            reader.ReadNull();
                        }
                        else
                        {
                            url = reader.ReadString();
                        }

                        break;
                    case "events":
                        events = ReadEvents(reader);
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndDocument();

            return new ImageModelVersionWebhookConfig
            {
                Url = url,
                Events = events
            };
        }

        private static List<string>? ReadEvents(IBsonReader reader)
        {
            if (reader.GetCurrentBsonType() == BsonType.Null)
            {
                reader.ReadNull();
                return null;
            }

            var values = new List<string>();
            reader.ReadStartArray();

            while (reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                if (reader.GetCurrentBsonType() == BsonType.String)
                {
                    values.Add(reader.ReadString());
                }
                else
                {
                    reader.SkipValue();
                }
            }

            reader.ReadEndArray();
            return values;
        }
    }
}
