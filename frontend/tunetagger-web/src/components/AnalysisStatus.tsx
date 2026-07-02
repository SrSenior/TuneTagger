type AnalysisStatusType =
  | 'idle'
  | 'file-selected'
  | 'analyzing'
  | 'matched'
  | 'not-found'
  | 'error'

type AnalysisStatusProps = {
  status: AnalysisStatusType
  message?: string
}

const statusContent: Record<
  AnalysisStatusType,
  {
    label: string
    description: string
    className: string
  }
> = {
  idle: {
    label: 'Waiting for a file',
    description: 'Select an audio file to begin.',
    className: 'border-slate-800 bg-slate-900 text-slate-300',
  },
  'file-selected': {
    label: 'File ready',
    description: 'The selected file is ready to be analyzed.',
    className: 'border-cyan-900/70 bg-cyan-950/30 text-cyan-200',
  },
  analyzing: {
    label: 'Analyzing',
    description: 'TuneTagger is generating a fingerprint and checking AcoustID.',
    className: 'border-amber-900/70 bg-amber-950/30 text-amber-200',
  },
  matched: {
    label: 'Match found',
    description: 'TuneTagger found a possible match for this track.',
    className: 'border-emerald-900/70 bg-emerald-950/30 text-emerald-200',
  },
  'not-found': {
    label: 'No match found',
    description: 'No reliable match was found for this audio file.',
    className: 'border-slate-700 bg-slate-900 text-slate-300',
  },
  error: {
    label: 'Error',
    description: 'Something went wrong while analyzing the file.',
    className: 'border-red-900/70 bg-red-950/30 text-red-200',
  },
}

function AnalysisStatus({ status, message }: AnalysisStatusProps) {
  const content = statusContent[status]

  return (
    <section className={`rounded-2xl border px-5 py-4 ${content.className}`}>
      <p className="font-semibold">{content.label}</p>

      <p className="mt-1 text-sm opacity-80">
        {message ?? content.description}
      </p>
    </section>
  )
}

export default AnalysisStatus