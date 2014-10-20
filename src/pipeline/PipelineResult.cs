using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace tsql2pgsql.pipeline
{
    public class PipelineResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the director should rebuild the pipeline.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [rebuild pipeline]; otherwise, <c>false</c>.
        /// </value>
        public bool RebuildPipeline { get; set; }

        /// <summary>
        /// When set allows the pipeline visitor to tell the pipeline director to use the
        /// specified contents as the new pipeline data.
        /// </summary>
        /// <value>
        /// The contents.
        /// </value>
        public IEnumerable<string> Contents { get; set; } 
    }
}
