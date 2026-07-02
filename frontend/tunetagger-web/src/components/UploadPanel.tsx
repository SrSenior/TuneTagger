type UploadPanelProps = {
    selectedFile: File | null
    isAnalyzing: boolean
    onFileChange: (file: File | null) => void
    onAnalyze: () => void
}

function UploadPanel({
    selectedFile,
    isAnalyzing,
    onFileChange,
    onAnalyze,
}: UploadPanelProps) {
    return (
        <section className="rounded-3xl border border-slate-800 bg-slate-900/80 p-6 shadow-2xl shadow-black/20">
            <div>
                <p className="text-sm font-medium text-cyan-400">Audio analysis</p>

                <h2 className="mt-2 text-2xl font-bold text-white">
                    Upload a track
                </h2>

                <p className="mt-2 text-sm text-slate-400">
                    Select a local audio file and TuneTagger will try to identify it.
                </p>
            </div>

            <label className="mt-6 flex cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed border-slate-700 bg-slate-950/60 px-6 py-10 text-center transition hover:border-cyan-500 hover:bg-slate-950">
                <span className="text-lg font-semibold text-white">
                    Choose an audio file
                </span>

                <span className="mt-2 text-sm text-slate-400">
                    Supported formats: MP3, WAV, FLAC, M4A, OGG
                </span>

                <input
                    className="hidden"
                    type="file"
                    accept=".mp3,.wav,.flac,.m4a,.ogg"
                    onChange={(event) => {
                        const file = event.target.files?.[0] ?? null
                        onFileChange(file)
                    }}
                />
            </label>

        
            {selectedFile && (
                <div className="mt-5 rounded-2xl border border-slate-800 bg-slate-950 px-4 py-3">
                    <p className="text-xs uppercase tracking-wide text-slate-500">
                        Selected file
                    </p>

                    <p className="mt-1 break-all text-sm font-medium text-slate-200">
                        {selectedFile.name}
                    </p>
                </div>
            )}

            <button
                type="button"
                disabled={!selectedFile || isAnalyzing}
                onClick={onAnalyze}
                className="mt-6 w-full rounded-2xl bg-cyan-500 px-5 py-3 font-semibold text-slate-950 transition hover:bg-cyan-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
            >
                {isAnalyzing ? 'Analyzing...' : 'Analyze file'}
            </button>
        </section>
    )
}

export default UploadPanel