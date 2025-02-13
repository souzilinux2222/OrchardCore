using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;

namespace OrchardCore.DisplayManagement.Liquid
{
    /// <summary>
    /// An <see cref="IHtmlContent"/> implementation that inherits from <see cref="TextWriter"/> to write to the ASP.NET ViewBufferTextWriter
    /// in an optimal way.
    /// </summary>
    public class ViewBufferTextWriterContent : TextWriter, IHtmlContent
    {
        private StringBuilder _builder;
        private StringBuilderPool _pooledBuilder;
        private List<StringBuilderPool> _previousPooledBuilders;

        public override Encoding Encoding => Encoding.UTF8;

        public ViewBufferTextWriterContent()
        {
            _pooledBuilder = StringBuilderPool.GetInstance();
            _builder = _pooledBuilder.Builder;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            ReleasePooledBuffer();
        }

        private void ReleasePooledBuffer()
        {
            if (_pooledBuilder != null)
            {
                _pooledBuilder.Dispose();
                _pooledBuilder = null;
                _builder = null;

                if (_previousPooledBuilders != null)
                {
                    foreach (var pooledBuilder in _previousPooledBuilders)
                    {
                        pooledBuilder.Dispose();
                    }

                    _previousPooledBuilders.Clear();
                    _previousPooledBuilders = null;
                }
            }
        }

        private StringBuilder AllocateBuilder()
        {
            _previousPooledBuilders ??= new List<StringBuilderPool>();
            _previousPooledBuilders.Add(_pooledBuilder);

            _pooledBuilder = StringBuilderPool.GetInstance();
            _builder = _pooledBuilder.Builder;
            return _builder;
        }

        // Invoked when used as TextWriter to intercept what is supposed to be written
        public override void Write(string value)
        {
            if (value == null || value.Length == 0)
            {
                return;
            }

            if (_builder.Length + value.Length <= _builder.Capacity)
            {
                _builder.Append(value);
            }
            else
            {
                // The string doesn't fit in the buffer, rent more
                var index = 0;
                do
                {
                    var sizeToCopy = Math.Min(_builder.Capacity - _builder.Length, value.Length - index);
                    _builder.Append(value.AsSpan(index, sizeToCopy));

                    if (_builder.Length == _builder.Capacity)
                    {
                        AllocateBuilder();
                    }

                    index += sizeToCopy;
                } while (index < value.Length);
            }
        }

        public override void Write(char value)
        {
            if (_builder.Length >= _builder.Capacity)
            {
                AllocateBuilder();
            }

            _builder.Append(value);
        }

        public override void Write(char[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return;
            }

            if (_builder.Length + buffer.Length <= _builder.Capacity)
            {
                _builder.Append(buffer);
            }
            else
            {
                // The string doesn't fit in the buffer, rent more
                var index = 0;
                do
                {
                    var sizeToCopy = Math.Min(_builder.Capacity - _builder.Length, buffer.Length - index);
                    _builder.Append(buffer.AsSpan(index, sizeToCopy));

                    if (_builder.Length == _builder.Capacity)
                    {
                        AllocateBuilder();
                    }

                    index += sizeToCopy;
                } while (index < buffer.Length);
            }
        }

        public override void Write(char[] buffer, int offset, int count)
        {
            if (buffer == null || buffer.Length == 0 || count == 0)
            {
                return;
            }

            if (_builder.Length + count <= _builder.Capacity)
            {
                _builder.Append(buffer, offset, count);
            }
            else
            {
                // The string doesn't fit in the buffer, rent more
                var index = 0;
                do
                {
                    var sizeToCopy = Math.Min(_builder.Capacity - _builder.Length, count - index);
                    _builder.Append(buffer.AsSpan(index + offset, sizeToCopy));

                    if (_builder.Length == _builder.Capacity)
                    {
                        AllocateBuilder();
                    }

                    index += sizeToCopy;
                } while (index < count);
            }
        }

        public override void Write(ReadOnlySpan<char> buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            if (_builder.Length + buffer.Length <= _builder.Capacity)
            {
                _builder.Append(buffer);
            }
            else
            {
                // The string doesn't fit in the buffer, rent more
                var index = 0;
                do
                {
                    var sizeToCopy = Math.Min(_builder.Capacity - _builder.Length, buffer.Length - index);
                    _builder.Append(buffer.Slice(index, sizeToCopy));

                    if (_builder.Length == _builder.Capacity)
                    {
                        AllocateBuilder();
                    }

                    index += sizeToCopy;
                } while (index < buffer.Length);
            }
        }

        public void WriteTo(TextWriter writer, HtmlEncoder encoder)
        {
            if (_builder == null)
            {
                throw new InvalidOperationException("Buffer has already been rendered");
            }

            if (_previousPooledBuilders != null)
            {
                foreach (var pooledBuilder in _previousPooledBuilders)
                {
                    foreach (var chunk in pooledBuilder.Builder.GetChunks())
                    {
                        writer.Write(chunk.Span);
                    }
                }
            }

            foreach (var chunk in _builder.GetChunks())
            {
                writer.Write(chunk.Span);
            }

            ReleasePooledBuffer();
        }

        public override Task FlushAsync()
        {
            // Override since the base implementation does unnecessary work
            return Task.CompletedTask;
        }
    }
}
