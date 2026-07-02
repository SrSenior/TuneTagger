import { useState } from 'react'
import UploadPanel from './components/UploadPanel'
import AnalysisStatus from './components/AnalysisStatus'
import TrackResultCard from './components/TrackResultCard'

type AnalysisStatusType =
  | 'idle'
  | 'file-selected'
  | 'analyzing'
  | 'matched'
  | 'not-found'
  | 'error'

type TrackAnalysisResult = {
  originalFileName: string
  title: string
  artist: string
  album: string
  suggestedFileName: string
  confidence: number
  status: string
}

const allowedExtensions = ['.mp3', '.wav', '.flac', '.m4a', '.ogg']

function App() {
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [status, setStatus] = useState<AnalysisStatusType>('idle')
  const [statusMessage, setStatusMessage] = useState<string | undefined>()
  const [result, setResult] = useState<TrackAnalysisResult | null>(null)

  function handleFileChange(file: File | null) {
    setSelectedFile(file)
    setResult(null)
    setStatusMessage(undefined)

    if (!file) {
      setStatus('idle')
      return
    }

    const fileExtension = getFileExtension(file.name)

    if (!allowedExtensions.includes(fileExtension)) {
      setStatus('error')
      setStatusMessage(`Unsupported file format: ${fileExtension || 'unknown'}`)
      return
    }

    setStatus('file-selected')
  }

  async function handleAnalyze() {
    if (!selectedFile) {
      setStatus('error')
      setStatusMessage('Please select a file before analyzing.')
      return
    }

    const fileExtension = getFileExtension(selectedFile.name)

    if (!allowedExtensions.includes(fileExtension)) {
      setStatus('error')
      setStatusMessage(`Unsupported file format: ${fileExtension || 'unknown'}`)
      return
    }

    setStatus('analyzing')
    setStatusMessage(undefined)
    setResult(null)

    await new Promise((resolve) => setTimeout(resolve, 900))

    const mockResult: TrackAnalysisResult = {
      originalFileName: selectedFile.name,
      title: 'Wild Side',
      artist: 'ALI',
      album: 'Wild Side',
      suggestedFileName: `ALI - Wild Side${fileExtension}`,
      confidence: 0.97,
      status: 'matched',
    }

    setResult(mockResult)
    setStatus('matched')
  }

  function handleAccept() {
    setStatus('matched')
    setStatusMessage('Suggestion accepted. Rename/apply action is not implemented yet.')
  }

  function handleCancel() {
    setSelectedFile(null)
    setResult(null)
    setStatus('idle')
    setStatusMessage(undefined)
  }

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-8 text-white">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-8">
        <header className="rounded-3xl border border-slate-800 bg-slate-900/60 p-8 shadow-2xl shadow-black/20">
          <p className="text-sm font-semibold uppercase tracking-[0.3em] text-cyan-400">
            Local-first audio tagging
          </p>

          <h1 className="mt-4 text-4xl font-black tracking-tight text-white md:text-5xl">
            TuneTagger
          </h1>

          <p className="mt-4 max-w-2xl text-slate-400">
            Identify local audio files, retrieve clean metadata and generate a
            better suggested file name before applying changes.
          </p>
        </header>

        <div className="grid gap-6 lg:grid-cols-[1fr_1.2fr]">
          <div className="flex flex-col gap-6">
            <UploadPanel
              selectedFile={selectedFile}
              isAnalyzing={status === 'analyzing'}
              onFileChange={handleFileChange}
              onAnalyze={handleAnalyze}
            />

            <AnalysisStatus status={status} message={statusMessage} />
          </div>

          <div>
            {result ? (
              <TrackResultCard
                result={result}
                onAccept={handleAccept}
                onCancel={handleCancel}
              />
            ) : (
              <section className="flex min-h-full items-center justify-center rounded-3xl border border-dashed border-slate-800 bg-slate-900/40 p-8 text-center">
                <div>
                  <p className="text-lg font-semibold text-slate-300">
                    No analysis result yet
                  </p>

                  <p className="mt-2 max-w-md text-sm text-slate-500">
                    Upload a supported audio file and run the analysis to see
                    detected metadata here.
                  </p>
                </div>
              </section>
            )}
          </div>
        </div>
      </div>
    </main>
  )
}

function getFileExtension(fileName: string) {
  const dotIndex = fileName.lastIndexOf('.')

  if (dotIndex === -1) {
    return ''
  }

  return fileName.slice(dotIndex).toLowerCase()
}

export default App