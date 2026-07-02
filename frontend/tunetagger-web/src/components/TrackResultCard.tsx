type TrackAnalysisResult = {
  originalFileName: string
  title: string
  artist: string
  album: string
  suggestedFileName: string
  confidence: number
  status: string
}

type TrackResultCardProps = {
  result: TrackAnalysisResult
  onAccept: () => void
  onCancel: () => void
}

function TrackResultCard({
  result,
  onAccept,
  onCancel,
}: TrackResultCardProps) {
  const confidencePercentage = Math.round(result.confidence * 100)

  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900/80 p-6 shadow-2xl shadow-black/20">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-sm font-medium text-emerald-400">
            Suggested match
          </p>

          <h2 className="mt-2 text-3xl font-bold text-white">
            {result.title}
          </h2>

          <p className="mt-1 text-slate-400">
            {result.artist}
          </p>
        </div>

        <div className="rounded-full border border-emerald-900 bg-emerald-950 px-3 py-1 text-sm font-semibold text-emerald-300">
          {confidencePercentage}%
        </div>
      </div>

      <div className="mt-6 grid gap-4 md:grid-cols-2">
        <div className="rounded-2xl bg-slate-950 p-4">
          <p className="text-xs uppercase tracking-wide text-slate-500">
            Album
          </p>
          <p className="mt-1 text-sm font-medium text-slate-200">
            {result.album}
          </p>
        </div>

        <div className="rounded-2xl bg-slate-950 p-4">
          <p className="text-xs uppercase tracking-wide text-slate-500">
            Original file
          </p>
          <p className="mt-1 break-all text-sm font-medium text-slate-200">
            {result.originalFileName}
          </p>
        </div>
      </div>

      <div className="mt-4 rounded-2xl border border-cyan-900/60 bg-cyan-950/20 p-4">
        <p className="text-xs uppercase tracking-wide text-cyan-400">
          Suggested file name
        </p>

        <p className="mt-1 break-all text-sm font-semibold text-cyan-100">
          {result.suggestedFileName}
        </p>
      </div>

      <div className="mt-6 flex flex-col gap-3 sm:flex-row">
        <button
          type="button"
          onClick={onAccept}
          className="flex-1 rounded-2xl bg-emerald-500 px-5 py-3 font-semibold text-slate-950 transition hover:bg-emerald-400"
        >
          Accept suggestion
        </button>

        <button
          type="button"
          onClick={onCancel}
          className="flex-1 rounded-2xl border border-slate-700 px-5 py-3 font-semibold text-slate-300 transition hover:border-slate-500 hover:bg-slate-800"
        >
          Cancel
        </button>
      </div>
    </section>
  )
}

export default TrackResultCard