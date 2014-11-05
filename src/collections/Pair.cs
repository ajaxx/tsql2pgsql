namespace tsql2pgsql.collections
{
    /// <summary>
    /// Construct for pair of objects
    /// </summary>
    /// <typeparam name="TA">The type of a.</typeparam>
    /// <typeparam name="TB">The type of the b.</typeparam>
    public class Pair<TA,TB>
    {
        /// <summary>
        /// Gets or sets the A value.
        /// </summary>
        public TA A { get; set; }
        /// <summary>
        /// Gets or sets the B value.
        /// </summary>
        public TB B { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pair{TA, TB}"/> class.
        /// </summary>
        public Pair()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pair{TA, TB}"/> class.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        public Pair(TA a, TB b)
        {
            A = a;
            B = b;
        } 
    }
}
