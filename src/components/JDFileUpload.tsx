import { useState, type ChangeEvent } from "react";
import { FileUp, LoaderCircle } from "lucide-react";
import { api } from "../api";

// The backend extracts text from PDF, DOCX and TXT; it never executes document content.
export default function JDFileUpload({ onText, onError }: {
  onText: (content: string, fileName: string) => void;
  onError: (message: string) => void;
}) {
  const [loading, setLoading] = useState(false);
  const [fileName, setFileName] = useState("");

  async function handleFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    if (!/\.(pdf|docx|txt)$/i.test(file.name)) { onError("JD must be PDF, DOCX or TXT."); return; }
    if (file.size > 5 * 1024 * 1024) { onError("JD cannot exceed 5 MB."); return; }
    setLoading(true);
    try {
      const document = await api.readDocument(file);
      setFileName(document.fileName);
      onText(document.text, document.fileName);
    } catch (error) {
      onError(error instanceof Error ? error.message : "Unable to read the JD.");
    } finally { setLoading(false); }
  }

  return <div className="jd-file-control">
    <label className="jd-upload-label">
      {loading ? <LoaderCircle size={17} className="spin"/> : <FileUp size={17}/>}
      {loading ? "Reading job description..." : "Upload JD (PDF, Word or TXT)"}
      <input type="file" accept=".pdf,.docx,.txt" disabled={loading} onChange={e => void handleFile(e)}/>
    </label>
    {fileName && <p className="jd-upload-file" title={fileName}>Imported: {fileName} · editable below</p>}
  </div>;
}
