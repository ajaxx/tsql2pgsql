using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Common.Logging;

namespace tsql2pgsql.pipeline
{
    internal class PipelineDirector
    {
        /// <summary>
        /// Logger for instance
        /// </summary>
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The pipeline
        /// </summary>
        private Pipeline _pipeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineDirector"/> class.
        /// </summary>
        /// <param name="initialPipelineContents">The initial pipeline contents.</param>
        public PipelineDirector(string[] initialPipelineContents)
        {
            _pipeline = new Pipeline(initialPipelineContents);
        }

        /// <summary>
        /// Gets the content of the processed data.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> Contents
        {
            get { return string.Join("\n", _pipeline.Contents).Split('\n'); }
        }

        /// <summary>
        /// Processes the specified visitor.
        /// </summary>
        /// <param name="visitor">The visitor.</param>
        /// <returns></returns>
        public PipelineDirector Process(PipelineVisitor visitor)
        {
            var savePipeline = visitor.Pipeline;

            try
            {
                Log.DebugFormat("Process: Executing {0} pipeline", visitor.GetType().FullName);

                visitor.Pipeline = _pipeline;
                var pipelineResult = visitor.Visit(_pipeline);
                if (pipelineResult.Contents != null)
                {
                    _pipeline = new Pipeline(
                        string.Join("\n", pipelineResult.Contents).Split('\n'));
                }
                else if (pipelineResult.RebuildPipeline)
                {
                    _pipeline = new Pipeline(
                        string.Join("\n", _pipeline.Contents).Split('\n'));
                }

                return this;
            }
            finally
            {
                visitor.Pipeline = savePipeline;
            }
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public PipelineDirector Process<T>() where T : PipelineVisitor
        {
            return Process(Activator.CreateInstance<T>());
        }
    }
}
